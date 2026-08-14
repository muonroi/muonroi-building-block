namespace Muonroi.BuildingBlock.IntegrationTests;

/// <summary>
/// Test web application factory with in-memory services and JWT configuration.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>Test tenant identifier.</summary>
    public string TestTenantId { get; set; } = "test-tenant-001";
    /// <summary>Test user identifier.</summary>
    public string TestUserId { get; set; } = Guid.NewGuid().ToString();
    /// <summary>Test username.</summary>
    public string TestUsername { get; set; } = "testuser@example.com";
    /// <summary>Test permission bitmask.</summary>
    public long TestUserPermissions { get; set; } = 0b1111111111;
    /// <summary>JWT signing key used in tests.</summary>
    public string JwtSecretKey { get; set; } = "test-secret-key-minimum-32-characters-long-for-hs256";
    /// <summary>JWT issuer used in tests.</summary>
    public string JwtIssuer { get; set; } = "test-issuer";
    /// <summary>JWT audience used in tests.</summary>
    public string JwtAudience { get; set; } = "test-audience";

    /// <summary>
    /// Configures the test web host and test services.
    /// </summary>
    /// <param name="builder">Web host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSolutionRelativeContentRoot("tests/Muonroi.BuildingBlock.IntegrationTests");

        builder.ConfigureTestServices(services =>
        {
            ServiceDescriptor? descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<TestDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<TestDbContext>(options => options.UseInMemoryDatabase("IntegrationTestDb"));

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = JwtIssuer,
                    ValidAudience = JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecretKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });

            services.RemoveAll<ITenantIdResolver>();
            services.AddScoped<ITenantIdResolver, TestTenantIdResolver>();
            services.AddScoped<ITenantContext, TenantContext>();
        });
    }

    /// <summary>
    /// Generates a JWT token for integration tests.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="username">User name.</param>
    /// <param name="permissions">Permission bitmask.</param>
    /// <param name="tenantId">Optional tenant identifier.</param>
    /// <param name="expiration">Optional token expiration.</param>
    /// <returns>Serialized JWT token.</returns>
    public string GenerateJwtToken(
        string userId,
        string username,
        long permissions,
        string? tenantId = null,
        TimeSpan? expiration = null)
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityTokenHandler tokenHandler = new();
        byte[] key = Encoding.UTF8.GetBytes(JwtSecretKey);
        List<Claim> claims =
        [
            new Claim(ClaimConstants.UserIdentifier, userId),
            new Claim(ClaimConstants.Username, username),
            new Claim(ClaimConstants.Permission, permissions.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim("Permission", permissions.ToString()),
            new Claim("CurrentUserGuid", userId),
            new Claim("CurrentUsername", username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        if (!string.IsNullOrEmpty(tenantId))
        {
            claims.Add(new Claim(ClaimConstants.TenantId, tenantId));
            claims.Add(new Claim("TenantId", tenantId));
        }

        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.AddMinutes(-5),
            IssuedAt = now,
            Expires = now.Add(expiration ?? TimeSpan.FromHours(1)),
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

/// <summary>
/// In-memory EF Core context used by integration tests.
/// </summary>
/// <param name="options">Context options.</param>
public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    /// <summary>Users table.</summary>
    public DbSet<TestUser> Users { get; set; } = null!;
    /// <summary>Tenants table.</summary>
    public DbSet<TestTenant> Tenants { get; set; } = null!;
}

/// <summary>
/// Test user entity.
/// </summary>
public class TestUser
{
    /// <summary>User identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>User name.</summary>
    public string Username { get; set; } = string.Empty;
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;
    /// <summary>Permission bitmask.</summary>
    public long Permissions { get; set; }
    /// <summary>Whether the user is active.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Test tenant entity.
/// </summary>
public class TestTenant
{
    /// <summary>Tenant primary key.</summary>
    public int Id { get; set; }
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;
    /// <summary>Tenant name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Whether the tenant is active.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Resolves tenant identifiers for integration tests.
/// </summary>
public class TestTenantIdResolver : ITenantIdResolver
{
    /// <summary>
    /// Resolves tenant id from headers or returns a default.
    /// </summary>
    /// <param name="context">HTTP context.</param>
    /// <returns>Resolved tenant identifier.</returns>
    public Task<string?> ResolveTenantIdAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue))
        {
            string value = headerValue.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return Task.FromResult<string?>(value.Trim());
            }
        }

        return Task.FromResult<string?>("test-tenant-001");
    }
}
