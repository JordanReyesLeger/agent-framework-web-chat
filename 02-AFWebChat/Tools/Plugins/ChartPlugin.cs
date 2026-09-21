using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AFWebChat.Tools.Plugins;

/// <summary>
/// Genera especificaciones de gráficos (formato Chart.js) a partir de datos numéricos.
/// La función no dibuja nada en el servidor: devuelve un bloque Markdown con el JSON de
/// configuración envuelto en ```chart ... ```. El frontend (chat.js) detecta ese bloque y lo
/// convierte en un &lt;canvas&gt; renderizado con Chart.js, visible directamente en el chat.
///
/// Tipos soportados:
///  - Por categoría (necesitan "labelsJson"): bar, horizontalBar, stackedBar, line, area,
///    stackedLine, pie, doughnut, radar, polarArea.
///  - Por coordenadas (ignoran "labelsJson", usa []): scatter (datos {x,y}) y
///    bubble (datos {x,y,r}).
///  - Combinados: si chartType es "bar" y uno de los datasets trae su propio "type" (p.ej.
///    "line"), ese dataset se dibuja distinto al resto (útil para barras + línea de tendencia).
/// </summary>
public class ChartPlugin
{
    // Alias case-insensitive → nombre canónico. Acepta variantes comunes (donut/doughnut,
    // guiones, mayúsculas) que un LLM podría usar sin que fallen en un tipo inválido.
    private static readonly Dictionary<string, string> TypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bar"] = "bar",
        ["horizontalbar"] = "horizontalBar",
        ["horizontal-bar"] = "horizontalBar",
        ["stackedbar"] = "stackedBar",
        ["stacked-bar"] = "stackedBar",
        ["line"] = "line",
        ["area"] = "area",
        ["stackedline"] = "stackedLine",
        ["stacked-line"] = "stackedLine",
        ["stackedarea"] = "stackedLine",
        ["pie"] = "pie",
        ["doughnut"] = "doughnut",
        ["donut"] = "doughnut",
        ["radar"] = "radar",
        ["polararea"] = "polarArea",
        ["polar-area"] = "polarArea",
        ["scatter"] = "scatter",
        ["bubble"] = "bubble",
    };

    private static readonly HashSet<string> XyTypes = ["scatter", "bubble"];
    private static readonly HashSet<string> MultiSliceTypes = ["pie", "doughnut", "polarArea"];
    private static readonly HashSet<string> BarLikeOrPointTypes = ["bar", "horizontalBar", "stackedBar", "scatter", "bubble"];
    private static readonly HashSet<string> AreaTypes = ["area", "stackedLine"];
    private static readonly HashSet<string> StackedTypes = ["stackedBar", "stackedLine"];

    // Paleta de colores consistente con el branding por defecto de la app (ver AppBrandingSettings).
    private static readonly string[] Palette =
    [
        "#0078d4", "#ff8c00", "#8764b8", "#00b294", "#e74856",
        "#ffb900", "#498205", "#c239b3", "#0099bc", "#767676"
    ];

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    [Description("""
        Genera un gráfico y devuelve un bloque listo para visualizarse en el chat. Copia el
        resultado de esta función TAL CUAL, en su propia línea dentro de tu respuesta — no
        reescribas ni resumas el bloque ```chart``` que devuelve, o el gráfico no se renderizará.

        Tipos disponibles:
        - "bar": comparar categorías. "horizontalBar": igual, pero barras horizontales (útil con
          muchas categorías o etiquetas largas). "stackedBar": barras apiladas por categoría.
        - "line": tendencia. "area": línea con relleno bajo la curva. "stackedLine": áreas apiladas.
        - "pie" / "doughnut": proporciones de un total (máx. ~8 categorías). "polarArea": similar,
          con énfasis en magnitud.
        - "radar": comparar varias dimensiones de una o pocas entidades.
        - "scatter": correlación entre dos variables numéricas (ignora labelsJson, usa "[]").
        - "bubble": como scatter, pero con un tercer valor (tamaño de la burbuja).

        Para un gráfico combinado (p.ej. barras + línea de tendencia), usa chartType="bar" y
        agrega "type":"line" dentro del dataset que quieras mostrar como línea.
        """)]
    public static string GenerateChart(
        [Description("Tipo de gráfico: bar, horizontalBar, stackedBar, line, area, stackedLine, pie, doughnut, radar, polarArea, scatter o bubble")] string chartType,
        [Description("Título del gráfico")] string title,
        [Description("""
            Etiquetas del eje X o categorías, en formato JSON. Ej: ["Ene","Feb","Mar"].
            No aplica para "scatter"/"bubble" — en ese caso pasa "[]".
            """)] string labelsJson,
        [Description("""
            Series de datos en formato JSON (arreglo de objetos con "label" y "data").
            - Tipos por categoría: [{"label":"Ventas 2024","data":[10,20,30]}]
            - "scatter": [{"label":"Serie A","data":[{"x":1,"y":5},{"x":2,"y":8}]}]
            - "bubble": [{"label":"Serie A","data":[{"x":1,"y":5,"r":10}]}]
            Puedes incluir "backgroundColor"/"borderColor" propios por dataset; si no, se
            asignan automáticamente de una paleta consistente.
            """)] string datasetsJson)
    {
        var requestedType = TypeAliases.TryGetValue((chartType ?? "").Trim(), out var canonical) ? canonical : "bar";
        var isXy = XyTypes.Contains(requestedType);

        JsonArray? labelsArray;
        JsonArray? datasetsArray;
        try
        {
            labelsArray = isXy
                ? []
                : (string.IsNullOrWhiteSpace(labelsJson) ? [] : JsonNode.Parse(labelsJson) as JsonArray);
            datasetsArray = string.IsNullOrWhiteSpace(datasetsJson) ? [] : JsonNode.Parse(datasetsJson) as JsonArray;
        }
        catch (JsonException ex)
        {
            return $"Error: no se pudo interpretar los datos del gráfico ({ex.Message}). " +
                   "Verifica que labelsJson y datasetsJson sean JSON válido.";
        }

        if (datasetsArray is not { Count: > 0 })
        {
            return "Error: se necesita al menos una serie de datos (datasetsJson) para generar el gráfico.";
        }

        if (!isXy && labelsArray is not { Count: > 0 })
        {
            return "Error: este tipo de gráfico necesita etiquetas (labelsJson).";
        }

        var isMultiSlice = MultiSliceTypes.Contains(requestedType);
        var isArea = AreaTypes.Contains(requestedType);
        var useSolidBackground = BarLikeOrPointTypes.Contains(requestedType);

        for (var i = 0; i < datasetsArray.Count; i++)
        {
            if (datasetsArray[i] is not JsonObject ds) continue;

            if (isMultiSlice)
            {
                if (!ds.ContainsKey("backgroundColor") && labelsArray is not null)
                {
                    var sliceColors = new JsonArray();
                    for (var idx = 0; idx < labelsArray.Count; idx++) sliceColors.Add(Palette[idx % Palette.Length]);
                    ds["backgroundColor"] = sliceColors;
                }
                continue;
            }

            if (ds.ContainsKey("backgroundColor")) continue; // respeta el color si el modelo ya lo puso

            var color = Palette[i % Palette.Length];
            ds["backgroundColor"] = useSolidBackground ? color : $"{color}33";
            ds["borderColor"] = color;
            ds["borderWidth"] = requestedType is "scatter" or "bubble" ? 1 : 2;
            if (requestedType is "line" or "area" or "stackedLine") ds["tension"] = 0.3;
            if (isArea) ds["fill"] = true;
        }

        // Traduce nuestros alias a lo que Chart.js realmente entiende (type + opciones extra).
        var options = new JsonObject
        {
            ["plugins"] = new JsonObject
            {
                ["title"] = new JsonObject { ["display"] = !string.IsNullOrWhiteSpace(title), ["text"] = title }
            }
        };

        string chartJsType;
        switch (requestedType)
        {
            case "horizontalBar":
                chartJsType = "bar";
                options["indexAxis"] = "y";
                break;
            case "area":
                chartJsType = "line";
                break;
            default:
                chartJsType = StackedTypes.Contains(requestedType) ? (requestedType == "stackedBar" ? "bar" : "line") : requestedType;
                break;
        }

        if (StackedTypes.Contains(requestedType))
        {
            options["scales"] = new JsonObject
            {
                ["x"] = new JsonObject { ["stacked"] = true },
                ["y"] = new JsonObject { ["stacked"] = true }
            };
        }

        var config = new JsonObject
        {
            ["type"] = chartJsType,
            ["data"] = new JsonObject
            {
                ["labels"] = labelsArray ?? [],
                ["datasets"] = datasetsArray
            },
            ["options"] = options
        };

        return $"```chart\n{config.ToJsonString(WriteOptions)}\n```";
    }
}
