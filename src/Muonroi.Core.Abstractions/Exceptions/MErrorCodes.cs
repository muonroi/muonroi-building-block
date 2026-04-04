namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Centralized registry of structured error codes for the Muonroi Building Block ecosystem.
/// Format: MBB:{PACKAGE_ABBR}:{NNN} where NNN is a 3-digit per-package sequential number.
/// </summary>
/// <remarks>
/// Error codes provide machine-readable identifiers for all known exception conditions.
/// They appear in logs, API error responses, and monitoring dashboards.
/// Package abbreviations: CORE, RULE, AUTH, TNCY, GRPC, DATA, GOV, MEDR, BG, UI, ASP
/// </remarks>
public static class MErrorCodes
{
    /// <summary>
    /// Error codes for the Muonroi.Core and Muonroi.Core.Abstractions packages.
    /// Package abbreviation: CORE
    /// </summary>
    public static class Core
    {
        /// <summary>Cross-tenant rule execution detected — TenantId mismatch between rule set and execution context.</summary>
        public const string CrossTenantExecution = "MBB:CORE:001";

        /// <summary>Internal invariant violated — a "should never happen" condition was triggered.</summary>
        public const string InvariantViolated = "MBB:CORE:002";

        /// <summary>Required dependency or service was null at injection point.</summary>
        public const string NullDependency = "MBB:CORE:003";
    }

    /// <summary>
    /// Error codes for the Muonroi.RuleEngine.* packages (Runtime, Core, Runtime.Web).
    /// Package abbreviation: RULE
    /// </summary>
    public static class Rule
    {
        /// <summary>Unsupported FEEL syntax node encountered during expression tree compilation.</summary>
        public const string UnsupportedFeelSyntax = "MBB:RULE:001";

        /// <summary>Unsupported comparison operator encountered in FEEL expression compilation.</summary>
        public const string UnsupportedComparisonOperator = "MBB:RULE:002";

        /// <summary>Unsupported arithmetic operator encountered in FEEL expression compilation.</summary>
        public const string UnsupportedArithmeticOperator = "MBB:RULE:003";

        /// <summary>Unsupported FEEL comparison operation string in FeelExpressionCompiler.</summary>
        public const string UnsupportedFeelComparison = "MBB:RULE:004";

        /// <summary>ExecuteCodeWorkflowBridgeAsync method bridge is missing or not registered.</summary>
        public const string CodeWorkflowBridgeMissing = "MBB:RULE:005";

        /// <summary>ExecuteLegacyWorkflowBridgeAsync method bridge is missing or not registered.</summary>
        public const string LegacyWorkflowBridgeMissing = "MBB:RULE:006";

        /// <summary>ExecuteFlowGraphBridgeAsync method bridge is missing or not registered.</summary>
        public const string FlowGraphBridgeMissing = "MBB:RULE:007";

        /// <summary>Cross-tenant rule execution detected in RulesEngineService — TenantId mismatch.</summary>
        public const string CrossTenantRuleExecution = "MBB:RULE:008";

        /// <summary>Rule set version file not found at the expected path.</summary>
        public const string RuleSetVersionNotFound = "MBB:RULE:009";

        /// <summary>Required IRuleSetStore dependency was null.</summary>
        public const string NullRuleSetStore = "MBB:RULE:010";

        /// <summary>Required IRuleSetDefinitionValidator dependency was null.</summary>
        public const string NullRuleSetValidator = "MBB:RULE:011";

        /// <summary>Required IServiceProvider dependency was null in dry-run service.</summary>
        public const string NullServiceProvider = "MBB:RULE:012";

        /// <summary>Required ISystemExecutionContextAccessor dependency was null in dry-run service.</summary>
        public const string NullExecutionContextAccessor = "MBB:RULE:013";

        /// <summary>Rule action delegate was null when creating Activation.</summary>
        public const string NullRuleAction = "MBB:RULE:014";
    }

    /// <summary>
    /// Error codes for the Muonroi.Auth and Muonroi.Auth.* packages.
    /// Package abbreviation: AUTH
    /// </summary>
    public static class Auth
    {
        /// <summary>Tenant context is required for this resource but was absent.</summary>
        public const string TenantContextRequired = "MBB:AUTH:001";

        /// <summary>Permission check failed — caller lacks required permission.</summary>
        public const string PermissionDenied = "MBB:AUTH:002";
    }

    /// <summary>
    /// Error codes for the Muonroi.Tenancy.* packages.
    /// Package abbreviation: TNCY
    /// </summary>
    public static class Tenancy
    {
        /// <summary>Tenant context was null when a valid tenant scope was expected.</summary>
        public const string NullTenantContext = "MBB:TNCY:001";

        /// <summary>Connection string for the specified tenant could not be resolved.</summary>
        public const string TenantConnectionStringMissing = "MBB:TNCY:002";

        /// <summary>Site work context or connection string was null during infrastructure setup.</summary>
        public const string SiteConnectionStringMissing = "MBB:TNCY:003";
    }

    /// <summary>
    /// Error codes for the Muonroi.Grpc package.
    /// Package abbreviation: GRPC
    /// </summary>
    public static class Grpc
    {
        /// <summary>gRPC context was null — required for interceptor processing.</summary>
        public const string NullGrpcContext = "MBB:GRPC:001";

        /// <summary>gRPC handler key not found in registered handler dictionary.</summary>
        public const string HandlerKeyNotFound = "MBB:GRPC:002";
    }

    /// <summary>
    /// Error codes for the Muonroi.Data.* packages (EntityFrameworkCore, Dapper).
    /// Package abbreviation: DATA
    /// </summary>
    public static class Data
    {
        /// <summary>MongoDB EF Core provider is not supported — use a supported relational provider.</summary>
        public const string MongoDbNotSupported = "MBB:DATA:001";

        /// <summary>Dapper string handler Parse method is not implemented.</summary>
        public const string DapperHandlerNotImplemented = "MBB:DATA:002";
    }

    /// <summary>
    /// Error codes for the Muonroi.Governance.* packages (Enterprise, Security).
    /// Package abbreviation: GOV
    /// </summary>
    public static class Governance
    {
        /// <summary>Anti-tamper sensors detected a security violation — debugger, profiler, or hooks present.</summary>
        public const string AntiTamperViolation = "MBB:GOV:001";

        /// <summary>Execution integrity was compromised — memory or stack manipulation detected.</summary>
        public const string ExecutionIntegrityCompromised = "MBB:GOV:002";

        /// <summary>Feature requires an enterprise license tier but current tier is insufficient.</summary>
        public const string EnterpriseLicenseRequired = "MBB:GOV:003";

        /// <summary>Assembly hash mismatch detected by CodeIntegrityVerifier.</summary>
        public const string AssemblyIntegrityViolation = "MBB:GOV:004";
    }

    /// <summary>
    /// Error codes for the Muonroi.Mediator package.
    /// Package abbreviation: MEDR
    /// </summary>
    public static class Mediator
    {
        /// <summary>Unauthorized access in mediator pipeline — request rejected by auth behavior.</summary>
        public const string UnauthorizedRequest = "MBB:MEDR:001";

        /// <summary>Forbidden access in mediator pipeline — caller lacks required permissions.</summary>
        public const string ForbiddenRequest = "MBB:MEDR:002";
    }

    /// <summary>
    /// Error codes for the Muonroi.BackgroundJobs.* packages.
    /// Package abbreviation: BG
    /// </summary>
    public static class BackgroundJobs
    {
        /// <summary>Expression-based job scheduling is not supported in the Quartz provider — use class-based jobs.</summary>
        public const string ExpressionJobNotSupported = "MBB:BG:001";

        /// <summary>Expression-based recurring job scheduling is not supported in the Quartz provider.</summary>
        public const string ExpressionRecurringJobNotSupported = "MBB:BG:002";

        /// <summary>Expression-based fire-and-forget jobs are not supported in the Quartz provider.</summary>
        public const string ExpressionFireAndForgetNotSupported = "MBB:BG:003";
    }

    /// <summary>
    /// Error codes for the Muonroi.UiEngine.* packages.
    /// Package abbreviation: UI
    /// </summary>
    public static class UiEngine
    {
        /// <summary>ContextType argument was null when constructing BindRuleContextAttribute.</summary>
        public const string NullContextType = "MBB:UI:001";
    }

    /// <summary>
    /// Error codes for the Muonroi.AspNetCore and Muonroi.AspNetCore.* packages.
    /// Package abbreviation: ASP
    /// </summary>
    public static class AspNetCore
    {
        /// <summary>Encryption must be enabled in Production environment with a paid license tier.</summary>
        public const string EncryptionRequiredInProduction = "MBB:ASP:001";
    }
}
