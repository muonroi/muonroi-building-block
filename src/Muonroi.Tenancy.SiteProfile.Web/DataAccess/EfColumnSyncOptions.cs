namespace Muonroi.Tenancy.SiteProfile.Web.DataAccess;

/// <summary>
/// Configuration options for EF-to-Dapper column mapping sync.
/// </summary>
public sealed class EfColumnSyncOptions
{
    /// <summary>
    /// When true, EfColumnSyncHostedService runs at startup to populate ISiteColumnMap
    /// from EF IModel metadata. Default: false (opt-in per D-12).
    /// </summary>
    public bool SyncColumnMappingFromEfModel { get; set; }
}
