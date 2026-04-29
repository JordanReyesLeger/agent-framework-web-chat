namespace AFWebChat.Models;

public class IndexConfiguration
{
    public required string IndexName { get; set; }
    public required string SemanticConfigName { get; set; }
    public required string VectorProfileName { get; set; }
    public int VectorDimensions { get; set; } = 1024;
    public List<IndexFieldDefinition> Fields { get; set; } = new();
}

public class IndexFieldDefinition
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public bool IsKey { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsFilterable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsFacetable { get; set; }
    public bool IsVector { get; set; }
}
