using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace AFWebChat.Tools.Plugins;

/// <summary>
/// Plugin para ejecutar consultas SQL de solo lectura (SELECT) en Azure SQL.
/// Implementa validaciones defensivas para garantizar la seguridad.
/// </summary>
public class QuerySqlPlugin
{
    private readonly string? _connectionString;
    private readonly ILogger<QuerySqlPlugin> _logger;
    private const int MaxRows = 100000;
    private const int TimeoutSeconds = 15;

    public QuerySqlPlugin(IConfiguration configuration, ILogger<QuerySqlPlugin> logger)
    {
        _connectionString = configuration.GetConnectionString("SqlServer");
        _logger = logger;
    }

    [Description("Ejecuta una consulta SQL de solo lectura (SELECT) en la base de datos y devuelve resultados en formato JSON. Solo se permiten consultas SELECT.")]
    public async Task<string> QuerySql(
        [Description("Consulta SQL SELECT a ejecutar. No se permiten INSERT, UPDATE, DELETE, DROP ni otras operaciones de modificación.")] string sqlQuery)
    {
        if (string.IsNullOrEmpty(_connectionString))
            return "La conexión a SQL Server no está configurada.";

        var validationError = ValidateSqlQuery(sqlQuery);
        if (validationError != null)
            return $"Error de validación: {validationError}";

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sqlQuery, connection);
            command.CommandTimeout = TimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync();
            var results = new List<Dictionary<string, object?>>();
            int rowCount = 0;

            while (await reader.ReadAsync() && rowCount < MaxRows)
            {
                var row = new Dictionary<string, object?>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);

                    if (value is DateTime dt)
                        row[columnName] = dt.ToString("yyyy-MM-dd HH:mm:ss");
                    else if (value is byte[] bytes)
                        row[columnName] = $"<binary data, {bytes.Length} bytes>";
                    else
                        row[columnName] = value;
                }

                results.Add(row);
                rowCount++;
            }

            var response = new
            {
                RowCount = rowCount,
                MaxRowsReached = rowCount >= MaxRows,
                Data = results
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error SQL ejecutando consulta");
            return $"Error de SQL: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando consulta");
            return $"Error al ejecutar la consulta: {ex.Message}";
        }
    }

    [Description("Ejecuta una consulta SQL de solo lectura (SELECT) y devuelve los resultados en formato tabular legible. Solo se permiten consultas SELECT.")]
    public async Task<string> QuerySqlTabular(
        [Description("Consulta SQL SELECT a ejecutar. Solo se permiten consultas SELECT.")] string sqlQuery)
    {
        if (string.IsNullOrEmpty(_connectionString))
            return "La conexión a SQL Server no está configurada.";

        var validationError = ValidateSqlQuery(sqlQuery);
        if (validationError != null)
            return $"Error de validación: {validationError}";

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sqlQuery, connection);
            command.CommandTimeout = TimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync();
            var sb = new StringBuilder();

            // Headers
            var headers = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
                headers.Add(reader.GetName(i));

            sb.AppendLine(string.Join(" | ", headers));
            sb.AppendLine(new string('-', headers.Sum(h => h.Length) + (headers.Count - 1) * 3));

            // Rows
            int rowCount = 0;
            while (await reader.ReadAsync() && rowCount < MaxRows)
            {
                var values = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString();
                    values.Add(value ?? "NULL");
                }
                sb.AppendLine(string.Join(" | ", values));
                rowCount++;
            }

            sb.AppendLine();
            sb.AppendLine($"Filas devueltas: {rowCount}");
            if (rowCount >= MaxRows)
                sb.AppendLine($"ADVERTENCIA: Se alcanzó el límite máximo de {MaxRows} filas.");

            return sb.ToString();
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error SQL ejecutando consulta tabular");
            return $"Error de SQL: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando consulta tabular");
            return $"Error al ejecutar la consulta: {ex.Message}";
        }
    }

    private static string? ValidateSqlQuery(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            return "La consulta SQL no puede estar vacía.";

        var trimmed = sqlQuery.Trim();
        var upper = trimmed.ToUpperInvariant();

        if (trimmed.EndsWith(';'))
        {
            trimmed = trimmed[..^1].TrimEnd();
            upper = trimmed.ToUpperInvariant();
        }

        if (trimmed.Contains(';'))
            return "No se permiten múltiples sentencias SQL. Solo se permite una consulta SELECT a la vez.";

        if (!upper.StartsWith("SELECT"))
            return "Solo se permiten consultas SELECT.";

        var prohibited = new[]
        {
            "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER",
            "TRUNCATE", "EXEC", "EXECUTE", "SP_", "XP_",
            "GRANT", "REVOKE", "DENY"
        };

        foreach (var keyword in prohibited)
        {
            if (Regex.IsMatch(upper, $@"\b{keyword}\b"))
                return $"La consulta contiene la operación prohibida '{keyword}'. Solo se permiten consultas SELECT.";
        }

        return null;
    }
}
