namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Represents a scoping unit within a connector platform — e.g. a Jira project (key "TTOS")
/// or a Confluence space (key "DEV"). The BA selects a scope to narrow document browse results
/// to a specific project or space.
/// </summary>
/// <param name="Id">
/// Platform-native scope key (e.g. Jira project key "TTOS", Confluence space key "DEV").
/// Sent as-is to <see cref="ConnectorBrowseQuery.Scope"/> — never mutated.
/// </param>
/// <param name="Label">
/// Human-readable display label shown in the picker dropdown
/// (e.g. "Tổng thể Operations Scrum (TTOS)"). Typically "&lt;name&gt; (&lt;key&gt;)".
/// </param>
public sealed record ConnectorScope(string Id, string Label);
