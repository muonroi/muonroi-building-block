namespace Quickstart.Data.Dapper.Api.Controllers;

/// <summary>
/// Exercises the parts of Muonroi.Data.Dapper that do not require a live database
/// connection: the <see cref="MDapperCommand"/> wrapper, the cross-tenant
/// <see cref="DapperRlsBypass"/> scope, and the static RLS provider/guarantee model.
///
/// In a real service you would resolve <c>IDapper</c> (registered by the package's
/// <c>AddDapperForXxx</c> + <c>AddMuonroiDapperRls()</c>) and pass an
/// <see cref="MDapperCommand"/> to the QueryPageAsync/QueryPlainPageAsync extension
/// methods in <c>MDapperExtensions</c>. That path requires a database, so it is
/// described here rather than executed.
/// </summary>
[ApiController]
[Route("api/dapper")]
public class DapperController : ControllerBase
{
    // ---------------------------------------------------------------------------
    // 1. MDapperCommand — the command wrapper passed to MDapperExtensions
    //    GET /api/dapper/command
    //
    //    MDapperCommand holds CommandText, Parameters (DynamicParameters), an
    //    optional transaction, CommandType and CommandFlags. MDapperRepositoryBase
    //    builds one via CreateCommand() and auto-injects the TenantId parameter.
    //    Build() converts it to a Dapper CommandDefinition.
    // ---------------------------------------------------------------------------
    [HttpGet("command")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult BuildCommand()
    {
        var parameters = new DynamicParameters();
        parameters.Add("ProductId", 42);
        parameters.Add("TenantId", "tenant-abc");

        var command = new MDapperCommand
        {
            CommandText = "SELECT * FROM products WHERE id = @ProductId AND tenant_id = @TenantId",
            Parameters = parameters,
            CommandType = System.Data.CommandType.Text
        };

        // Build() produces the Dapper CommandDefinition actually executed by IDapper.
        global::Dapper.CommandDefinition definition = command.Build(HttpContext.RequestAborted);

        return Ok(new
        {
            command.CommandText,
            command.CommandType,
            commandFlags = command.CommandFlag.ToString(),
            parameterNames = parameters.ParameterNames,
            built = new { definition.CommandText, definition.CommandType },
            note = "Pass this MDapperCommand to IDapper.QueryPageAsync / QueryPlainPageAsync " +
                   "(MDapperExtensions) when a real connection is available."
        });
    }

    // ---------------------------------------------------------------------------
    // 2. DapperRlsBypass — ambient cross-tenant bypass scope
    //    POST /api/dapper/bypass-scope
    //
    //    Inside DapperRlsBypass.Enter() the PostgreSQL session-context setter issues
    //    SET ROLE <bypassRoleName> (BYPASSRLS) instead of SET app.current_tenant_id,
    //    so queries run cross-tenant. IsActive flows across async/await via AsyncLocal.
    // ---------------------------------------------------------------------------
    [HttpPost("bypass-scope")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult DemonstrateBypassScope()
    {
        bool beforeScope = DapperRlsBypass.IsActive;

        bool insideScope;
        using (IBypassScope scope = DapperRlsBypass.Enter())
        {
            insideScope = DapperRlsBypass.IsActive;
            _ = scope; // disposed at end of using → clears the ambient flag
        }

        bool afterScope = DapperRlsBypass.IsActive;

        return Ok(new
        {
            beforeScope,   // false
            insideScope,   // true  — RLS bypassed, queries run cross-tenant
            afterScope,    // false — scope disposed, isolation restored
            note = "Every bypassed connection open is audit-logged by the session-context setter."
        });
    }

    // ---------------------------------------------------------------------------
    // 3. DapperRls provider + guarantee model
    //    GET /api/dapper/rls-providers
    //
    //    DapperRlsOptions selects the provider (bound from MultiTenantConfigs:DapperRls)
    //    and RlsGuaranteeLevel describes enforcement strength. PostgreSQL & MSSQL are
    //    Native (engine-level); MySQL is Emulated and deferred.
    // ---------------------------------------------------------------------------
    [HttpGet("rls-providers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ShowRlsProviders()
    {
        var defaults = new DapperRlsOptions();

        return Ok(new
        {
            sectionName = DapperRlsOptions.SectionName, // "MultiTenantConfigs:DapperRls"
            defaults = new
            {
                provider = defaults.Provider.ToString(),                 // PostgreSql
                bypassRoleName = defaults.BypassRoleName,                // app_rls_bypass
                strictMode = defaults.StrictMode,                        // false
                verifyRlsObjectsOnStartup = defaults.VerifyRlsObjectsOnStartup
            },
            providers = new[]
            {
                new { provider = nameof(DapperRlsProvider.PostgreSql), guarantee = nameof(RlsGuaranteeLevel.Native) },
                new { provider = nameof(DapperRlsProvider.MsSql),      guarantee = nameof(RlsGuaranteeLevel.Native) },
                new { provider = nameof(DapperRlsProvider.MySql),      guarantee = nameof(RlsGuaranteeLevel.Emulated) }
            },
            note = "Enable RLS by setting MultiTenantConfigs:EnableRowLevelSecurity=true and calling " +
                   "services.AddMuonroiDapperRls() AFTER AddDapperForXxx. IRlsGuaranteeProvider then " +
                   "reports the configured level at runtime."
        });
    }
}
