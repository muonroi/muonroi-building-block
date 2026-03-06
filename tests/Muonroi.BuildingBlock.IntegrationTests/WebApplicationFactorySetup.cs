using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Tenancy.Abstractions;
using Muonroi.Tenancy.Abstractions.Interfaces;
using Muonroi.Tenancy.Core;

namespace Muonroi.BuildingBlock.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string TestTenantId { get; set; } = "test-tenant-001";
    public string TestUserId { get; set; } = Guid.NewGuid().ToString();
    public string TestUsername { get; set; } = "testuser@example.com";
    public long TestUserPermissions { get; set; } = 0b1111111111;
    public string JwtSecretKey { get; set; } = "test-secret-key-minimum-32-characters-long-for-hs256";
    public string JwtIssuer { get; set; } = "test-issuer";
    public string JwtAudience { get; set; } = "test-audience";

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

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestUser> Users { get; set; } = null!;
    public DbSet<TestTenant> Tenants { get; set; } = null!;
}

public class TestUser
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public long Permissions { get; set; }
    public bool IsActive { get; set; }
}

public class TestTenant
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class TestTenantIdResolver : ITenantIdResolver
{
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
