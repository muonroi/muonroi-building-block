namespace Quickstart.AuthZ.Api.Models;

/// <summary>
/// Authorization check request. Maps directly onto AuthorizationRuleContext,
/// the set of facts the rule engine evaluates.
/// </summary>
public sealed record AccessCheckRequest(
    string UserId,
    string TenantId,
    string Resource,
    string Action,
    string[] Roles);
