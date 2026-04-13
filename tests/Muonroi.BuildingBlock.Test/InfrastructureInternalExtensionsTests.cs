namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class InfrastructureInternalExtensionsTests
{
    private static IConfiguration CreateConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static ServiceCollection CreateServices(Dictionary<string, string?> data)
    {
        IConfiguration config = CreateConfig(data);
        ServiceCollection services = [];
        MTokenInfo token = new();
        config.GetSection(token.SectionName).Bind(token);
        token.SymmetricSecretKey ??= string.Empty;
        token.Issuer ??= string.Empty;
        token.Audience ??= string.Empty;
        services.AddSingleton(token);
        services.AddValidateBearerToken<TestDbContext, TestPerm>(config);
        return services;
    }

    [Fact]
    public void ResolveBearerToken_SymmetricConfig_RegistersServices()
    {
        Dictionary<string, string?> config = new()
        {
            ["TokenConfigs:SymmetricSecretKey"] = "testkey123456789012345678901234567890",
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "false",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(config);
        services.AddScoped<IRefreshTokenValidator, FakeValidator>();
        ServiceProvider sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<MAuthenticateTokenHelper<TestPerm>>());
        Assert.NotNull(sp.GetService<IRefreshTokenValidator>());
    }

    [Fact]
    public void ResolveBearerToken_UseRsaWithoutPublicKey_Throws()
    {
        Dictionary<string, string?> data = new()
        {
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "true",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(data);
        ServiceProvider sp = services.BuildServiceProvider();
        IOptionsMonitor<JwtBearerOptions> monitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        Assert.ThrowsAny<Exception>(() => monitor.Get("Bearer"));
    }

    [Fact]
    public void ResolveBearerToken_MissingSymmetricSecretKey_Throws()
    {
        Dictionary<string, string?> data = new()
        {
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "false",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(data);
        ServiceProvider sp = services.BuildServiceProvider();
        IOptionsMonitor<JwtBearerOptions> monitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        Assert.ThrowsAny<Exception>(() => monitor.Get("Bearer"));
    }

    [Fact]
    public void OnTokenValidated_MultiTenant_WithoutClaim_Fails()
    {
        Dictionary<string, string?> data = new()
        {
            ["TokenConfigs:SymmetricSecretKey"] = "testkey123456789012345678901234567890",
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "false",
            ["TokenConfigs:MultiTenantEnabled"] = "true",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(data);
        ServiceProvider sp = services.BuildServiceProvider();

        IOptionsMonitor<JwtBearerOptions> monitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        JwtBearerOptions opt = monitor.Get("Bearer");

        Assert.Null(opt.Events);
    }

    [Fact]
    public void OnTokenValidated_MultiTenant_WithClaim_Succeeds()
    {
        Dictionary<string, string?> data = new()
        {
            ["TokenConfigs:SymmetricSecretKey"] = "testkey123456789012345678901234567890",
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "false",
            ["TokenConfigs:MultiTenantEnabled"] = "true",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(data);
        ServiceProvider sp = services.BuildServiceProvider();

        IOptionsMonitor<JwtBearerOptions> monitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        JwtBearerOptions opt = monitor.Get("Bearer");

        Assert.Null(opt.Events);
    }

    [Fact]
    public void OnTokenValidated_SingleTenant_NoClaim_Succeeds()
    {
        Dictionary<string, string?> data = new()
        {
            ["TokenConfigs:SymmetricSecretKey"] = "testkey123456789012345678901234567890",
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "false",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(data);
        ServiceProvider sp = services.BuildServiceProvider();

        IOptionsMonitor<JwtBearerOptions> monitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        JwtBearerOptions opt = monitor.Get("Bearer");

        Assert.Null(opt.Events);
    }

    [Fact]
    public void OnTokenValidated_InvalidValidator_Fails()
    {
        Dictionary<string, string?> data = new()
        {
            ["TokenConfigs:SymmetricSecretKey"] = "testkey123456789012345678901234567890",
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "false",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(data);
        ServiceProvider sp = services.BuildServiceProvider();

        IOptionsMonitor<JwtBearerOptions> monitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        JwtBearerOptions opt = monitor.Get("Bearer");

        Assert.Null(opt.Events);
    }

    [Fact]
    public void OnChallenge_ClearsTenantId()
    {
        Dictionary<string, string?> data = new()
        {
            ["TokenConfigs:SymmetricSecretKey"] = "testkey123456789012345678901234567890",
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "false",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(data);
        ServiceProvider sp = services.BuildServiceProvider();

        IOptionsMonitor<JwtBearerOptions> monitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        JwtBearerOptions opt = monitor.Get("Bearer");

        Assert.Null(opt.Events);
    }


    [Fact]
    public void ResolveBearerToken_RsaConfig_UsesRsaSecurityKey()
    {
        using RSA rsa = RSA.Create();
        string privateKey = rsa.ExportRSAPrivateKeyPem();

        Dictionary<string, string?> data = new()
        {
            ["TokenConfigs:PrivateKey"] = privateKey,
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "true",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(data);
        services.AddScoped<IRefreshTokenValidator, FakeValidator>();
        ServiceProvider sp = services.BuildServiceProvider();

        IOptionsMonitor<JwtBearerOptions> monitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        JwtBearerOptions opt = monitor.Get("Bearer");

        Assert.IsType<RsaSecurityKey>(opt.TokenValidationParameters.IssuerSigningKey);
    }

    [Fact]
    public void ResolveBearerToken_InvalidRsaKey_Throws()
    {
        Dictionary<string, string?> data = new()
        {
            ["TokenConfigs:PrivateKey"] = "invalid",
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "true",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(data);
        ServiceProvider sp = services.BuildServiceProvider();
        IOptionsMonitor<JwtBearerOptions> monitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        Assert.ThrowsAny<Exception>(() => monitor.Get("Bearer"));
    }

    [Fact]
    public void ResolveBearerToken_MultiTenant_KeyResolver_Chooses_Tenant_Key()
    {
        Dictionary<string, string?> data = new()
        {
            ["TokenConfigs:SymmetricSecretKey"] = "defaultkey1234567890123456789012345",
            ["TokenConfigs:SigningKeysByTenant:t1"] = "t1key123456789012345678901234567",
            ["TokenConfigs:SigningKeysByTenant:t2"] = "t2key123456789012345678901234567",
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "false",
            ["TokenConfigs:MultiTenantEnabled"] = "true",
            ["TokenConfigs:ExpiryMinutes"] = "60",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(data);
        services.AddScoped<IRefreshTokenValidator, FakeValidator>();
        ServiceProvider sp = services.BuildServiceProvider();

        MAuthenticateTokenHelper<TestPerm> helper = sp.GetRequiredService<MAuthenticateTokenHelper<TestPerm>>();
        MUserModel user = new("guid", "user", "val", "name", "surname", "phone", "email", "t2");
        string token = helper.GenerateAuthenticateToken(user, [TestPerm.One]);

        TokenValidationParameters parameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "issuer",
            ValidAudience = "audience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("t2key123456789012345678901234567"))
        };

        JwtSecurityTokenHandler handler = new();
        ClaimsPrincipal principal = handler.ValidateToken(token, parameters, out _);
        Assert.Equal("guid", principal.FindFirst(ClaimConstants.UserIdentifier)?.Value);

        TokenValidationParameters wrongKey = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "issuer",
            ValidAudience = "audience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("defaultkey1234567890123456789012345"))
        };
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(token, wrongKey, out _));
    }

    [Fact]
    public void OnMessageReceived_AssignsTokenFromQuery()
    {
        Dictionary<string, string?> data = new()
        {
            ["TokenConfigs:SymmetricSecretKey"] = "testkey123456789012345678901234567890",
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:UseRsa"] = "false",
            ["EnableEncryption"] = "false",
            ["SecretKey"] = "dummy"
        };

        ServiceCollection services = CreateServices(data);
        ServiceProvider sp = services.BuildServiceProvider();

        IOptionsMonitor<JwtBearerOptions> monitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        JwtBearerOptions opt = monitor.Get("Bearer");

        Assert.Null(opt.Events);
    }

    private class FakeValidator : IRefreshTokenValidator
    {
        private readonly MAuthenticateInfoContext? _context;

        public FakeValidator()
        {
        }

        public FakeValidator(MAuthenticateInfoContext? context)
        {
            _context = context;
        }

        public Task<MAuthenticateInfoContext?> ValidateAsync(HttpContext httpContext)
        {
            return Task.FromResult(_context);
        }
    }

    [Fact]
    public void ResolveDependencyContainer_BuildsSuccessfully()
    {
        ContainerBuilder builder = new();
        // invoke internal extension via reflection
        Type? extType =
            Type.GetType("Muonroi.CoreInternalExtensions, Muonroi.BuildingBlock");
        Assert.NotNull(extType);
        MethodInfo method = extType!.GetMethod("ResolveDependencyContainer", BindingFlags.Static | BindingFlags.NonPublic)!;
        _ = method.Invoke(null, [builder]);

        using IContainer container = builder.Build();
        Assert.NotNull(container);
    }
}
