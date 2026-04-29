namespace dwa_ver_val.ViewModels;

/// <summary>
/// Renders the lineage panel on Property/Details: parent (predecessor),
/// successor, subdivision children (when this property was subdivided),
/// and consolidation predecessors (when this property was created by
/// consolidating others).
/// </summary>
public class PropertyLineageViewModel
{
    public Property? Parent { get; set; }
    public Property? Successor { get; set; }
    public IReadOnlyList<Property> Children { get; set; } = Array.Empty<Property>();
    public IReadOnlyList<Property> ConsolidationPredecessors { get; set; } = Array.Empty<Property>();

    public bool HasAny =>
        Parent is not null
        || Successor is not null
        || Children.Count > 0
        || ConsolidationPredecessors.Count > 0;
}
