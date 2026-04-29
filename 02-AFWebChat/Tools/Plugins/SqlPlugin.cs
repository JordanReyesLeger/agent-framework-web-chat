using System.ComponentModel;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace AFWebChat.Tools.Plugins;

public class SqlPlugin
{
    private readonly string? _connectionString;
    private readonly ILogger<SqlPlugin> _logger;

    public SqlPlugin(IConfiguration config, ILogger<SqlPlugin> logger)
        : this(config, logger, "SqlServer") { }

    public SqlPlugin(IConfiguration config, ILogger<SqlPlugin> logger, string connectionStringName)
    {
        _connectionString = config.GetConnectionString(connectionStringName);
        _logger = logger;
    }

    [Description("Gets the complete database schema including all tables, columns, types, precision and scale. Returns structured JSON with metadata only, no sensitive data.")]
    public async Task<string> GetFullSchema()
    {
        if (string.IsNullOrEmpty(_connectionString))
            return "SQL Server connection not configured. This is a demo - configure a connection string to use this feature.";

        try
        {
            var schemaInfo = new List<SchemaTableInfo>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var query = @"
                SELECT 
                    t.TABLE_SCHEMA, t.TABLE_NAME,
                    c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE,
                    c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, c.NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.TABLES t
                INNER JOIN INFORMATION_SCHEMA.COLUMNS c 
                    ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION";

            await using var cmd = new SqlCommand(query, conn) { CommandTimeout = 15 };
            await using var reader = await cmd.ExecuteReaderAsync();

            var currentTable = "";
            SchemaTableInfo? current = null;

            while (await reader.ReadAsync())
            {
                var fullName = $"{reader.GetString(0)}.{reader.GetString(1)}";

                if (fullName != currentTable)
                {
                    currentTable = fullName;
                    current = new SchemaTableInfo
                    {
                        Schema = reader.GetString(0),
                        TableName = reader.GetString(1),
                        Columns = []
                    };
                    schemaInfo.Add(current);
                }

                current?.Columns.Add(ReadColumnInfo(reader));
            }

            return JsonSerializer.Serialize(schemaInfo, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting full database schema");
            return $"Error getting schema: {ex.Message}";
        }
    }

    [Description("Gets the schema of a specific database table including column names, types, nullability, precision and scale. Supports 'schema.table' format (e.g. 'dbo.Customers') or just 'Customers'.")]
    public async Task<string> GetSchema(
        [Description("The name of the table to get schema for (e.g. 'Customers' or 'dbo.Customers')")] string tableName)
    {
        if (string.IsNullOrEmpty(_connectionString))
            return "SQL Server connection not configured. This is a demo - configure a connection string to use this feature.";

        try
        {
            var parts = tableName.Split('.');
            var tableNameOnly = parts.Length > 1 ? parts[1] : parts[0];
            string? schemaFilter = parts.Length > 1 ? parts[0] : null;

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var query = @"
                SELECT 
                    t.TABLE_SCHEMA, t.TABLE_NAME,
                    c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE,
                    c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, c.NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.TABLES t
                INNER JOIN INFORMATION_SCHEMA.COLUMNS c 
                    ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                    AND t.TABLE_NAME = @TableName"
                + (schemaFilter != null ? " AND t.TABLE_SCHEMA = @SchemaName" : "")
                + " ORDER BY c.ORDINAL_POSITION";

            await using var cmd = new SqlCommand(query, conn) { CommandTimeout = 15 };
            cmd.Parameters.AddWithValue("@TableName", tableNameOnly);
            if (schemaFilter != null)
                cmd.Parameters.AddWithValue("@SchemaName", schemaFilter);

            await using var reader = await cmd.ExecuteReaderAsync();
            var schemaInfo = new List<SchemaTableInfo>();
            SchemaTableInfo? current = null;

            while (await reader.ReadAsync())
            {
                if (current == null)
                {
                    current = new SchemaTableInfo
                    {
                        Schema = reader.GetString(0),
                        TableName = reader.GetString(1),
                        Columns = []
                    };
                    schemaInfo.Add(current);
                }

                current.Columns.Add(ReadColumnInfo(reader));
            }

            if (schemaInfo.Count == 0)
                return $"Table '{tableName}' not found.";

            return JsonSerializer.Serialize(schemaInfo, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schema for table {TableName}", tableName);
            return $"Error getting schema: {ex.Message}";
        }
    }

    [Description("Executes a read-only SQL query (SELECT only) and returns the results")]
    public async Task<string> ExecuteQuery(
        [Description("The SQL SELECT query to execute")] string sql)
    {
        if (string.IsNullOrEmpty(_connectionString))
            return "SQL Server connection not configured.";

        // Security: only allow SELECT statements
        var trimmed = sql.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return "Only SELECT queries are allowed. For write operations, use the DataModifier agent.";

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = 30;

            await using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<string>();

            // Header
            var columns = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i)).ToList();
            results.Add(string.Join(" | ", columns));

            int rowCount = 0;
            while (await reader.ReadAsync() && rowCount < 100)
            {
                var values = Enumerable.Range(0, reader.FieldCount)
                    .Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "").ToList();
                results.Add(string.Join(" | ", values));
                rowCount++;
            }

            return rowCount == 0 ? "No results found." : string.Join("\n", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query");
            return $"Error executing query: {ex.Message}";
        }
    }

    [Description("Explains a SQL query by showing its execution plan")]
    public string ExplainQuery(
        [Description("The SQL query to explain")] string sql)
    {
        return $"Query explanation for: {sql}\n" +
               "This is a demo feature. In production, this would show the actual execution plan.";
    }

    [Description("Lists all tables in the database with their schema and approximate row count.")]
    public async Task<string> ListTables()
    {
        if (string.IsNullOrEmpty(_connectionString))
            return "SQL Server connection not configured.";

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var query = @"
                SELECT t.TABLE_SCHEMA, t.TABLE_NAME, 
                       (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS c 
                        WHERE c.TABLE_NAME = t.TABLE_NAME AND c.TABLE_SCHEMA = t.TABLE_SCHEMA) AS ColumnCount
                FROM INFORMATION_SCHEMA.TABLES t
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

            await using var cmd = new SqlCommand(query, conn) { CommandTimeout = 15 };
            await using var reader = await cmd.ExecuteReaderAsync();

            var lines = new List<string> { "Schema | Table | Columns" };
            while (await reader.ReadAsync())
                lines.Add($"{reader.GetString(0)} | {reader.GetString(1)} | {reader.GetInt32(2)}");

            return lines.Count > 1 ? string.Join("\n", lines) : "No tables found.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing tables");
            return $"Error listing tables: {ex.Message}";
        }
    }

    private static SchemaColumnInfo ReadColumnInfo(SqlDataReader reader) => new()
    {
        ColumnName = reader.GetString(2),
        DataType = reader.GetString(3),
        IsNullable = reader.GetString(4) == "YES",
        MaxLength = reader.IsDBNull(5) ? null : reader.GetInt32(5),
        NumericPrecision = reader.IsDBNull(6) ? null : (int?)reader.GetByte(6),
        NumericScale = reader.IsDBNull(7) ? null : reader.GetInt32(7)
    };
}
