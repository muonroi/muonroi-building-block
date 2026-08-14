namespace Muonroi.UiEngine.Catalog.Services;

internal static class CatalogSnapshotStoreHelper
{
    public static MUiEngineCatalogSnapshot CreateSnapshot(
        string tenantId,
        MUiEngineCatalogGraph graph,
        IMDateTimeService dateTimeService,
        IMJsonSerializeService jsonSerializeService)
    {
        MUiEngineCatalogGraph clonedGraph = CloneGraph(graph, jsonSerializeService);

        return new MUiEngineCatalogSnapshot
        {
            SnapshotId = Guid.NewGuid(),
            TenantId = NormalizeTenantId(tenantId),
            CapturedAt = dateTimeService.UtcNow(),
            Graph = clonedGraph,
            ApiCount = CountNodes(clonedGraph, "endpoint"),
            RuleCount = CountNodes(clonedGraph, "rule"),
            BindingCount = CountEdges(clonedGraph, "triggers")
        };
    }

    public static MUiEngineCatalogSnapshot CloneSnapshot(
        MUiEngineCatalogSnapshot snapshot,
        IMJsonSerializeService jsonSerializeService)
    {
        string json = jsonSerializeService.Serialize(snapshot);
        return jsonSerializeService.Deserialize<MUiEngineCatalogSnapshot>(json) ?? new MUiEngineCatalogSnapshot();
    }

    public static MUiEngineCatalogGraph CloneGraph(
        MUiEngineCatalogGraph graph,
        IMJsonSerializeService jsonSerializeService)
    {
        string json = jsonSerializeService.Serialize(graph);
        return jsonSerializeService.Deserialize<MUiEngineCatalogGraph>(json) ?? new MUiEngineCatalogGraph();
    }

    public static CatalogSnapshotSummary ToSummary(MUiEngineCatalogSnapshot snapshot)
    {
        return new CatalogSnapshotSummary
        {
            SnapshotId = snapshot.SnapshotId,
            TenantId = snapshot.TenantId,
            CapturedAt = snapshot.CapturedAt,
            ApiCount = snapshot.ApiCount,
            RuleCount = snapshot.RuleCount,
            BindingCount = snapshot.BindingCount
        };
    }

    public static string NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? "_global" : tenantId.Trim();
    }

    private static int CountNodes(MUiEngineCatalogGraph graph, string nodeType)
    {
        return graph.Nodes.Count(node => string.Equals(node.Type, nodeType, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountEdges(MUiEngineCatalogGraph graph, string edgeType)
    {
        return graph.Edges.Count(edge => string.Equals(edge.EdgeType, edgeType, StringComparison.OrdinalIgnoreCase));
    }
}
