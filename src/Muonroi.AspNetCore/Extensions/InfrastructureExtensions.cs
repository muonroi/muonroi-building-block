using Asp.Versioning;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using Muonroi.AspNetCore.Controllers;
using Muonroi.AspNetCore.Controllers.ActionFilters;
using Muonroi.AspNetCore.Controllers.Conventions;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.AspNetCore.DI.Autofac;
using Muonroi.AspNetCore.Filters;
using Muonroi.AspNetCore.Middleware;
using Muonroi.Core.Extensions;
using Muonroi.Tenancy.Core.Legacy;
using Muonroi.UiEngine.Catalog.Services;
using System.Security;
using System.Text.Json.Serialization;

namespace Muonroi.AspNetCore.Extensions;

/// <inheritdoc />
public static class InfrastructureExtensions
{
    private sealed class InfrastructureExtensionsLogger { }

/// <inheritdoc />
    public static readonly Assembly? EntryAssembly = Assembly.GetEntryAssembly();

/// <inheritdoc />
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        MTokenInfo? tokenConfig = null,
        MPaginationConfig? paginationConfigs = null,
        bool isSecretDefault = true,
        string secreteKey = "",
        params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            Assembly entryAssembly = EntryAssembly ?? Assembly.GetExecutingAssembly();
            assemblies = [entryAssembly];
        }

        services.AddSingleton(configuration);

        // Security: ProjectSeed is mandatory for fingerprint-based chaining
        string? projectSeed = configuration.GetValue<string>("LicenseConfigs:ProjectSeed");
        if (string.IsNullOrWhiteSpace(projectSeed) || projectSeed.Length < 16)
        {
            throw new MConfigurationException("[SEC_FATAL] ProjectSeed is missing or too weak. " +
                                                "A unique 16+ char seed is required for security chaining.", "LicenseConfigs:ProjectSeed");
        }

        // Scanning info + validation diagnostics are deferred and logged via IMLog<T>
        // on ApplicationStarted by ArchitectureDiagnosticsStartupService.
        string[] scannedAssemblyNames = [.. assemblies.Select(a => a.GetName().Name ?? "unknown")];
        ArchitectureValidationExtensions.AddStartupDiagnostic("Info",
            $"Framework scanning {assemblies.Length} assembly(ies): {string.Join(", ", scannedAssemblyNames)}");
        foreach (Assembly assembly in assemblies)
        {
            services.EnforceArchitecture(assembly);
        }
        _ = services.AddControllerConfiguration(assemblies)
            .AddLicenseProtection(configuration)
            .AddCoreServices(configuration, isSecretDefault, secreteKey, paginationConfigs, tokenConfig)
            .AddMultiLevelCaching(configuration)
            .AddAuthContext()
            .AddTenantContext(configuration)
            .AddTenantQuotaManagement()
            .AddMPolicyDecision(configuration);

        // ENTERPRISE SECURITY: Verify security requirements for paid licenses in production
        VerifyEnterpriseSecurityRequirements(configuration);

        services.AddApiVersioning(options =>
        {
            options.ReportApiVersions = true;
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.DefaultApiVersion = new ApiVersion(1, 0);
        }).AddMvc().AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        services.Configure<RouteOptions>(options =>
        {
            options.ConstraintMap.Add("apiVersion", typeof(Asp.Versioning.Routing.ApiVersionRouteConstraint));
        });

        // MCookieAuthMiddleware: activated by UseMiddleware<T>() (receives RequestDelegate from pipeline).
        // Do NOT register as scoped service — causes ValidateOnBuild failure since RequestDelegate isn't in DI.
        services.TryAddScoped<ICatalogScanService, NoopCatalogScanService>();
        services.TryAddSingleton<IUiEngineSchemaNotifier, NoopUiEngineSchemaNotifier>();
        services.TryAddScoped<IMControllerExecutionContextResolver, MDefaultControllerExecutionContextResolver>();
        _ = services.AddUiEngineChangePolicies();
        _ = services.AddHealthChecks();
        _ = services.AddEndpointsApiExplorer();

        return services;
    }
    internal static IServiceCollection AddAuthContext(this IServiceCollection services)
    {
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IAuthContextFactory, DefaultAuthContextFactory>();
        services.AddScoped(sp =>
        {
            IAuthContextFactory factory = sp.GetRequiredService<IAuthContextFactory>();
            return factory.Create();
        });
        return services;
    }
    internal static IServiceCollection AddControllerConfiguration(this IServiceCollection services, params Assembly[] assemblies)
    {
        // MAuthenticateInfoContext required by RequestLoggingFilter.
        // TryAdd = won't overwrite if consumer registers a real auth context (e.g. via JWT middleware).
        services.TryAddScoped(_ => new MAuthenticateInfoContext(false));
        services.AddScoped<RequestLoggingFilter>();
        _ = services.AddControllers(options =>
        {
            options.Filters.Add<GlobalExceptionFilter>();
            options.Filters.Add<RequestLoggingFilter>();
            options.Conventions.Add(new LowerCaseControllerNameConvention());
            options.Conventions.Add(new MControllerBaseConvention());
        })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddValidatorsFromAssemblies(assemblies);
        return services;
    }

/// <inheritdoc />
    public static IServiceCollection AddPermissionFilter<TPermission>(this IServiceCollection services)
        where TPermission : Enum
    {
        MGuard.NotNull(services);
        _ = services.AddScoped<PermissionFilter<TPermission>>();
        _ = services.AddMvc(options => { _ = options.Filters.AddService<PermissionFilter<TPermission>>(); });
        return services;
    }

/// <inheritdoc />
    public static IApplicationBuilder UseDefaultMiddleware(this IApplicationBuilder app)
    {
        MGuard.NotNull(app);
        IOptions<MultiTenantConfigs>? tenantOptions = app.ApplicationServices.GetService<IOptions<MultiTenantConfigs>>();
        if (tenantOptions?.Value.Enabled == true)
        {
            _ = app.UseMiddleware<TenantContextMiddleware>();
        }

        _ = app.UseQuotaEnforcement();
        _ = app.UseMiddleware<LicenseMiddleware>();
        _ = app.UseMiddleware<MExceptionMiddleware>();
        _ = app.UseMiddleware<MCookieAuthMiddleware>();
        return app;
    }

/// <inheritdoc />
    public static void AddAutofacConfiguration(this WebApplicationBuilder builder)
    {
        _ = builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        _ = builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            _ = containerBuilder.ResolveDependencyContainer();
        });
    }
    internal static ContainerBuilder ResolveDependencyContainer(this ContainerBuilder builder)
    {
        builder.RegisterModule(new MediatorModule());
        builder.RegisterModule(new AuthContextModule());
        return builder;
    }
/// <inheritdoc />
    public static IApplicationBuilder ConfigureEndpoints(this WebApplication app, bool mapHealthChecks = true)
    {
        MGuard.NotNull(app);
        _ = app.UseSwagger();
        _ = app.UseSwaggerUI();
        _ = app.MapControllers();
        if (mapHealthChecks)
        {
            _ = app.MapHealthChecks("/health");
            _ = app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = _ => false
            });
            _ = app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = _ => true
            });
            _ = app.MapGet("/grpc/ready", () => Results.Ok(new { status = "ready" }));
            _ = app.MapGet("/grpc/live", () => Results.Ok(new { status = "live" }));
        }
        _ = app.MapGet("/", context =>
        {
            context.Response.Redirect("/swagger");
            return Task.CompletedTask;
        });

        // Startup banner logged via IMLog<T> (maps to IMLog) after DI container is built.
        ICollection<string> serverAddresses = app.Urls;
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            IMLog<InfrastructureExtensionsLogger> startupLog =
                app.Services.GetRequiredService<IMLog<InfrastructureExtensionsLogger>>();
            string swagger = $"{(serverAddresses.Count > 0 ? serverAddresses.First() : "http://localhost:5000")}/swagger";
            startupLog.Info("Muonroi Building Block started. Addresses: {Addresses} | Swagger: {Swagger}",
                string.Join(", ", serverAddresses), swagger);
        });

        _ = app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                context.Response.ContentType = "application/json";
                Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature? exceptionHandlerPathFeature =
                    context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
                Exception? exception = exceptionHandlerPathFeature?.Error;
                int statusCode = exception switch
                {
                    UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                    KeyNotFoundException => StatusCodes.Status404NotFound,
                    ArgumentException => StatusCodes.Status400BadRequest,
                    InvalidOperationException => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status500InternalServerError
                };
                context.Response.StatusCode = statusCode;
                bool isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
                var response = new
                {
                    StatusCode = statusCode,
                    Error = new
                    {
                        Code = exception?.GetType().Name ?? "UnhandledException",
                        Message = isDev
                            ? exception?.Message ?? "An unexpected error occurred"
                            : "An error occurred while processing your request.",
                        Details = isDev ? exception?.InnerException?.Message : null
                    }
                };
                await context.Response.WriteAsJsonAsync(response);
            });
        });
        return app;
    }

/// <summary>
    /// Registers bearer token validation with JWT authentication.
    /// Signer implementations are internal — no dependency on Muonroi.Auth required.
    /// When IRefreshTokenValidator is not registered (Auth absent), refresh falls through to ClaimsPrincipal.
    /// </summary>
    public static IServiceCollection AddValidateBearerToken(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        MGuard.NotNull(services);
        MGuard.NotNull(configuration);
        services.TryAddSingleton<Func<IServiceProvider, HttpContext, Task<IAuthenticateInfoContext>>>(_ =>
            async (provider, httpContext) =>
            {
                IRefreshTokenValidator? validator = provider.GetService<IRefreshTokenValidator>();
                if (validator is null)
                {
                    return new MAuthenticateInfoContext(false);
                }

                IAuthenticateInfoContext? result = await validator.ValidateAsync(httpContext);
                return result ?? new MAuthenticateInfoContext(false);
            });
        services.TryAddSingleton<ITokenSigner>(sp =>
        {
            MTokenInfo configs = sp.GetRequiredService<MTokenInfo>();
            if (configs.UseRsa)
            {
                string privateKeyStr = configs.GetEffectivePrivateKey();
                if (string.IsNullOrWhiteSpace(privateKeyStr))
                {
                    throw new MConfigurationException(
                        "TokenConfigs.UseRsa is true but no RSA private key was provided. " +
                        "Set TokenConfigs:PrivateKey (inline PEM) or TokenConfigs:PrivateKeyPath (file path), " +
                        "or set TokenConfigs:UseRsa=false to use HMAC with SymmetricSecretKey.",
                        "TokenConfigs:PrivateKey");
                }
                RSA rsa = RSA.Create();
                rsa.ImportFromPem(privateKeyStr.ToCharArray());
                return new RsaTokenSigner(rsa);
            }

            if (string.IsNullOrWhiteSpace(configs.SymmetricSecretKey))
            {
                throw new MConfigurationException(
                    "TokenConfigs.UseRsa is false but TokenConfigs.SymmetricSecretKey is empty. " +
                    "Set TokenConfigs:SymmetricSecretKey or set TokenConfigs:UseRsa=true with a private key.",
                    "TokenConfigs:SymmetricSecretKey");
            }
            return new HmacTokenSigner(configs.SymmetricSecretKey);
        });
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            ServiceProvider sp = services.BuildServiceProvider();
            MTokenInfo tokenConfigs = sp.GetRequiredService<MTokenInfo>();
            ITokenSigner signer = sp.GetRequiredService<ITokenSigner>();
            SecurityKey defaultSigningKey = signer.GetCredentials().Key;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = tokenConfigs.Issuer,
                ValidAudience = tokenConfigs.Audience,
                IssuerSigningKey = defaultSigningKey,
                IssuerSigningKeyResolver = (_, _, kid, _) =>
                    {
                        if (tokenConfigs.UseRsa || string.IsNullOrWhiteSpace(kid))
                        {
                            return [defaultSigningKey];
                        }

                        if (tokenConfigs.SigningKeysByTenant.TryGetValue(kid, out string? tenantKey) &&
                            !string.IsNullOrWhiteSpace(tenantKey))
                        {
                            SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(tenantKey))
                            {
                                KeyId = kid
                            };
                            return [key, defaultSigningKey];
                        }

                        return [defaultSigningKey];
                    },
                ClockSkew = TimeSpan.Zero
            };
        });
        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Backward-compatible overload. Generic parameters are no longer needed —
    /// MAuthenticateTokenHelper and DefaultRefreshTokenValidator should be registered
    /// by the consuming application when Auth package is used.
    /// </summary>
    [Obsolete("Use AddValidateBearerToken(services, configuration) instead. Generic parameters are no longer needed.")]
    public static IServiceCollection AddValidateBearerToken<TDbContext, TPermission>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : MDbContext
        where TPermission : Enum
        => AddValidateBearerToken(services, configuration);

    /// <summary>
    /// Internal RSA token signer — decoupled from Muonroi.Auth.
    /// </summary>
    private sealed class RsaTokenSigner(RSA rsa) : ITokenSigner
    {
        public SigningCredentials GetCredentials()
        {
            SecurityKey key = new RsaSecurityKey(rsa);
            return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        }
    }

    /// <summary>
    /// Internal HMAC token signer — decoupled from Muonroi.Auth.
    /// </summary>
    private sealed class HmacTokenSigner(string signingKey) : ITokenSigner
    {
        public SigningCredentials GetCredentials()
        {
            SecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            return new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        }
    }

    private static void VerifyEnterpriseSecurityRequirements(IConfiguration configuration)
    {
        string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        bool isProduction = env.Equals("Production", StringComparison.OrdinalIgnoreCase);
        bool isStaging = env.Equals("Staging", StringComparison.OrdinalIgnoreCase);

        if (!isProduction && !isStaging)
        {
            return;
        }

        string? licenseMode = configuration.GetValue<string>("LicenseConfigs:Mode");
        string? licenseFilePath = configuration.GetValue<string>("LicenseConfigs:LicenseFilePath");
        bool enforceOnDatabase = configuration.GetValue<bool>("LicenseConfigs:EnforceOnDatabase");
        bool enforceOnMiddleware = configuration.GetValue<bool>("LicenseConfigs:EnforceOnMiddleware");

        bool hasLicenseFile = !string.IsNullOrWhiteSpace(licenseFilePath);
        bool hasEnforcement = enforceOnDatabase || enforceOnMiddleware;
        bool isPaidLicense = licenseMode?.Equals("Online", StringComparison.OrdinalIgnoreCase) == true ||
                           (hasLicenseFile && hasEnforcement);

        if (!isPaidLicense)
        {
            // Deferred to structured log on ApplicationStarted — avoids Console pollution.
            ArchitectureValidationExtensions.AddStartupDiagnostic("Warning",
                "Running FREE mode in Production — security enforcement is disabled.");
            return;
        }

        bool enableEncryption = configuration.GetValue<bool>("EnableEncryption");
        if (!enableEncryption)
        {
            throw new MInternalException("[SEC_FATAL] EnableEncryption must be true in Production with paid license.", MErrorCodes.AspNetCore.EncryptionRequired);
        }
    }
}
