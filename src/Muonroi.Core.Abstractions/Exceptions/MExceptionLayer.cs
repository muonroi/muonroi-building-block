namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Identifies the architectural layer where an exception originated.
/// Auto-derived from <see cref="MException.SourcePackage"/> via prefix matching.
/// Orthogonal to <see cref="MExceptionCategory"/> — Layer is "where", Category is "what kind".
/// </summary>
public enum MExceptionLayer
{
    /// <summary>Unrecognized or missing source package.</summary>
    Unknown = 0,
    /// <summary>HTTP/gRPC/BFF entry points (Muonroi.AspNetCore.*, Muonroi.Grpc.*, Muonroi.Bff.*, *.Web.*).</summary>
    Presentation = 1,
    /// <summary>Mediator pipeline and background jobs (Muonroi.Mediator.*, Muonroi.BackgroundJobs.*).</summary>
    Application = 2,
    /// <summary>Rule engine, tenancy, auth, governance, core (Muonroi.RuleEngine.*, Muonroi.Rules.*, Muonroi.Tenancy.*, Muonroi.Auth.*, Muonroi.AuthZ.*, Muonroi.Governance.*, Muonroi.Core.*).</summary>
    Domain = 3,
    /// <summary>Data, caching, messaging, logging, resilience, integration, HTTP, diagnostics.</summary>
    Infrastructure = 4
}
