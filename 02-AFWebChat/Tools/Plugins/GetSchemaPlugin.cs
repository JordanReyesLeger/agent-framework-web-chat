using System.ComponentModel;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace AFWebChat.Tools.Plugins;

/// <summary>
/// Plugin para obtener el esquema de la base de datos Azure SQL.
/// Solo devuelve metadatos estructurales (nombres de tablas, columnas, tipos).
/// </summary>
public class GetSchemaPlugin
{
    private readonly string? _connectionString;
    private readonly ILogger<GetSchemaPlugin> _logger;

    public GetSchemaPlugin(IConfiguration configuration, ILogger<GetSchemaPlugin> logger)
    {
        _connectionString = configuration.GetConnectionString("SqlServer");
        _logger = logger;
    }

    [Description("Obtiene el esquema completo de la base de datos incluyendo tablas, columnas y tipos de datos. No devuelve datos sensibles, solo metadatos estructurales.")]
    public async Task<string> GetSchema()
    {
        if (string.IsNullOrEmpty(_connectionString))
            return "La conexión a SQL Server no está configurada.";

        try
        {
            var schemaInfo = new List<SchemaTableInfo>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    t.TABLE_SCHEMA,
                    t.TABLE_NAME,
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.IS_NULLABLE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION,
                    c.NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.TABLES t
                INNER JOIN INFORMATION_SCHEMA.COLUMNS c 
                    ON t.TABLE_NAME = c.TABLE_NAME 
                    AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION";

            await using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 15;

            await using var reader = await command.ExecuteReaderAsync();
            var currentTable = "";
            SchemaTableInfo? currentSchema = null;

            while (await reader.ReadAsync())
            {
                var tableSchema = reader.GetString(0);
                var tableName = reader.GetString(1);
                var fullTableName = $"{tableSchema}.{tableName}";

                if (fullTableName != currentTable)
                {
                    currentTable = fullTableName;
                    currentSchema = new SchemaTableInfo
                    {
                        Schema = tableSchema,
                        TableName = tableName,
                        Columns = []
                    };
                    schemaInfo.Add(currentSchema);
                }

                currentSchema?.Columns.Add(new SchemaColumnInfo
                {
                    ColumnName = reader.GetString(2),
                    DataType = reader.GetString(3),
                    IsNullable = reader.GetString(4) == "YES",
                    MaxLength = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    NumericPrecision = reader.IsDBNull(6) ? null : (int?)reader.GetByte(6),
                    NumericScale = reader.IsDBNull(7) ? null : reader.GetInt32(7)
                });
            }

            return JsonSerializer.Serialize(schemaInfo, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo el esquema de la base de datos");
            return $"Error obteniendo el esquema: {ex.Message}";
        }
    }

    [Description("Obtiene el esquema de una tabla específica de la base de datos incluyendo columnas y tipos de datos.")]
    public async Task<string> GetTableSchema(
        [Description("Nombre de la tabla (puede incluir esquema, ej: 'dbo.Customers' o solo 'Customers')")] string tableName)
    {
        if (string.IsNullOrEmpty(_connectionString))
            return "La conexión a SQL Server no está configurada.";

        try
        {
            var schemaInfo = new List<SchemaTableInfo>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var parts = tableName.Split('.');
            var tableNameOnly = parts.Length > 1 ? parts[1] : parts[0];
            string? schemaNameFilter = parts.Length > 1 ? parts[0] : null;

            var query = @"
                SELECT 
                    t.TABLE_SCHEMA,
                    t.TABLE_NAME,
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.IS_NULLABLE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION,
                    c.NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.TABLES t
                INNER JOIN INFORMATION_SCHEMA.COLUMNS c 
                    ON t.TABLE_NAME = c.TABLE_NAME 
                    AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                    AND t.TABLE_NAME = @TableName"
                + (schemaNameFilter != null ? " AND t.TABLE_SCHEMA = @SchemaName" : "")
                + " ORDER BY c.ORDINAL_POSITION";

            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@TableName", tableNameOnly);
            if (schemaNameFilter != null)
                command.Parameters.AddWithValue("@SchemaName", schemaNameFilter);
            command.CommandTimeout = 15;

            await using var reader = await command.ExecuteReaderAsync();
            SchemaTableInfo? currentSchema = null;

            while (await reader.ReadAsync())
            {
                if (currentSchema == null)
                {
                    currentSchema = new SchemaTableInfo
                    {
                        Schema = reader.GetString(0),
                        TableName = reader.GetString(1),
                        Columns = []
                    };
                    schemaInfo.Add(currentSchema);
                }

                currentSchema.Columns.Add(new SchemaColumnInfo
                {
                    ColumnName = reader.GetString(2),
                    DataType = reader.GetString(3),
                    IsNullable = reader.GetString(4) == "YES",
                    MaxLength = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    NumericPrecision = reader.IsDBNull(6) ? null : (int?)reader.GetByte(6),
                    NumericScale = reader.IsDBNull(7) ? null : reader.GetInt32(7)
                });
            }

            if (schemaInfo.Count == 0)
                return $"No se encontró la tabla '{tableName}' en la base de datos.";

            return JsonSerializer.Serialize(schemaInfo, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo el esquema de la tabla {TableName}", tableName);
            return $"Error obteniendo el esquema de la tabla: {ex.Message}";
        }
    }
}

public class SchemaTableInfo
{
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<SchemaColumnInfo> Columns { get; set; } = [];
}

public class SchemaColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public int? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
}
