using MongoDB.Driver;
using Muonroi.AspNetCore.Controllers;
using Muonroi.BackgroundJobs.Abstractions;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Data.Abstractions.Repositories;
using Muonroi.Data.EntityFrameworkCore.Repositories;
using Muonroi.Http.Http;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.AspNetCore.Extensions;

/// <summary>
/// Architecture validation to ensure developers follow framework patterns.
/// Provides runtime warnings to guide developers toward best practices.
/// </summary>
public static class ArchitectureValidationExtensions
{
    private const string DocsBaseUrl = "https://github.com/muonroi/Muonroi.Docs";
    private static int _warningCount;
    private static int _infoCount;

    // Diagnostics are accumulated during DI-registration time (before the container is built,
    // IMLog<T> is not yet resolvable). They are flushed via ArchitectureDiagnosticsStartupService
    // on ApplicationStarted using proper IMLog<T> — maps to IMLog<T> in the ecosystem.
    private static readonly List<(string Level, string Message, string Supplemental, string Guide)> _pendingDiagnostics = [];

    /// <summary>
    /// Adds a startup diagnostic entry to be logged when the application starts.
    /// Allows other framework code to contribute diagnostics before the DI container is built.
    /// </summary>
    internal static void AddStartupDiagnostic(string level, string message, string supplemental = "", string guide = "")
    {
        _pendingDiagnostics.Add((level, message, supplemental, guide));
    }

    /// <summary>
    /// Validates architecture compliance for the given assembly.
    /// Call this during application startup to detect violations early.
    /// </summary>
    public static IServiceCollection EnforceArchitecture(this IServiceCollection services, Assembly assembly,
        bool throwOnViolation = false)
    {
        _warningCount = 0;
        _infoCount = 0;
        List<Type> types = [.. assembly.GetExportedTypes()];

        PrintHeader();

        // 1. Entity Layer Validation
        ValidateEntities(types);

        // 2. Repository Layer Validation
        ValidateRepositories(types);

        // 3. Query Layer Validation
        ValidateQueries(types);

        // 4. Controller Layer Validation
        ValidateControllers(types);

        // 5. Command/Query Handler Validation
        ValidateCommandHandlers(types);

        // 6. Event Layer Validation
        ValidateEvents(types);

        // 7. Background Job Validation
        ValidateBackgroundJobs(types);

        // 8. External Service Validation
        ValidateExternalServices(types);

        // 9. Multi-Tenant Validation
        ValidateMultiTenancy(types);

        // 10. DTO Validation
        ValidateDtos(types);

        // 11. Dependency Injection Best Practices
        ValidateDependencyInjection(types);

        // 12. Service Layer Validation
        ValidateServiceLayer(types);

        // 13. Framework Utilities Reminder
        PrintFrameworkUtilitiesInfo();

        PrintSummary(throwOnViolation);

        // Register the deferred diagnostic logger once — logged via IMLog<T> on ApplicationStarted.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ArchitectureDiagnosticsStartupService>());

        return services;
    }

    #region Entity Validation

    private static void ValidateEntities(List<Type> types)
    {
        // Find all classes that look like entities (in Entity/Entities folder or end with Entity)        
        List<Type> entities = [.. types.Where(t =>
            t is { IsClass: true, IsAbstract: false } &&
            (t.Name.EndsWith("Entity") ||
             t.Namespace?.Contains("Entity") == true ||
             t.Namespace?.Contains("Entities") == true ||
             t.Namespace?.Contains("Domain") == true) &&
            !t.Name.StartsWith('M') &&
            !typeof(MEntity).IsAssignableFrom(t)
        )];

        foreach (Type? entity in from entity in entities
                                 let hasIdProperty = entity.GetProperty("Id")
                                     != null || entity.GetProperty("EntityId") != null
                                 where hasIdProperty
                                 select entity)
        {
            PrintWarning(
                $"Entity '{entity.Name}' SHOULD inherit from 'MEntity'",
                "Auto Audit Trail, Soft-Delete, Snowflake ID, Multi-Tenant Security, Domain Events",
                $"{DocsBaseUrl}/backend-guide.md#1-entity"
            );
        }
    }

    #endregion

    #region Repository Validation

    private static void ValidateRepositories(List<Type> types)
    {
        List<Type> repositories = [.. types.Where(t =>
            t.Name.EndsWith("Repository") &&
            t is { IsClass: true, IsAbstract: false } &&
            !t.Name.StartsWith('M')
        )];

        foreach (Type? repo in from repo in repositories
                               let isMRepository = IsSubclassOfRawGeneric(typeof(MRepository<>),
                                   repo)
                               let implementsImRepository
                                   = ImplementsGenericInterface(repo, typeof(IMRepository<>))
                               where !isMRepository && !implementsImRepository
                               select repo)
        {
            PrintWarning(
                $"Repository '{repo.Name}' SHOULD inherit from 'MRepository<T>' or implement 'IMRepository<T>'",
                "Integrated UnitOfWork, Transaction Management, Specification Pattern, Bulk Operations",
                $"{DocsBaseUrl}/data-layer.md#repository-pattern"
            );
        }
    }

    #endregion

    #region Query Validation

    private static void ValidateQueries(List<Type> types)
    {
        // Find classes that look like database queries (not CQRS queries)
        List<Type> queries = [.. types.Where(t =>
                (t.Name.EndsWith("Queries") || t.Name.EndsWith("Query")) &&
                t is { IsClass: true, IsAbstract: false } &&
                !t.Name.StartsWith('M') && // Skip library queries
                // IMPORTANT: Exclude CQRS queries (MediatR IRequest<T>)
                // CQRS queries are commands/queries from API, not database queries
                !ImplementsGenericInterface(t, typeof(IRequest<>)) &&
                !typeof(IRequest).IsAssignableFrom(t) &&
                // Only warn for classes in Data/Queries/Repository folders
                (t.Namespace?.Contains("Data.Queries") == true ||
                 t.Namespace?.Contains("Queries.Dapper") == true ||
                 t.Namespace?.Contains("Persistence.Queries") == true ||
                 (t.Namespace?.EndsWith(".Queries") == true &&
                 !t.Namespace.Contains("Application") && // Not Application layer queries
                 !t.Namespace.Contains("Commands"))) // Not CQRS commands
        )];

        foreach (Type? query in from query in queries
                                let inheritsFromMQuery
                                    = IsSubclassOfRawGeneric(typeof(MQuery<>), query)
                                where !inheritsFromMQuery
                                select query)
        {
            PrintWarning(
                $"Database Query '{query.Name}' SHOULD inherit from 'MQuery<T>'",
                "Optimized Read Operations, Dapper Integration, Pagination Support, Query Caching",
                $"{DocsBaseUrl}/data-layer.md#query-pattern"
            );
        }
    }

    #endregion

    #region Controller Validation

    private static void ValidateControllers(List<Type> types)
    {
        List<Type> controllers = [.. types.Where(t =>
            t.Name.EndsWith("Controller") &&
            t is { IsClass: true, IsAbstract: false } &&
            typeof(ControllerBase).IsAssignableFrom(t) &&
            !t.Name.StartsWith('M') // Skip library controllers
        )];

        foreach (Type? controller in from controller in controllers
                                     let inheritsFromMControllerBase =
                                         typeof(MControllerBase).IsAssignableFrom(controller)
                                     let inheritsFromMAuthController =
                                         IsSubclassOfRawGeneric(typeof(MAuthControllerBase<,>), controller)
                                     where !inheritsFromMControllerBase && !inheritsFromMAuthController
                                     select controller)
        {
            PrintWarning(
                $"Controller '{controller.Name}' SHOULD inherit from 'MControllerBase' or 'MAuthControllerBase'",
                "Integrated Mediator Pattern, Logging, Mapping, Standard Response Format",
                $"{DocsBaseUrl}/backend-guide.md#3-controller"
            );
        }
    }

    #endregion

    #region Command Handler Validation

    private static void ValidateCommandHandlers(List<Type> types)
    {
        List<Type> handlers = [.. types.Where(t =>
            (t.Name.EndsWith("Handler") || t.Name.EndsWith("CommandHandler") || t.Name.EndsWith("QueryHandler")) &&
            t is { IsClass: true, IsAbstract: false } &&
            // Exclude external library handlers (ef CLI, MassTransit, etc.)
            !t.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true &&
            !t.Namespace?.StartsWith("MassTransit") == true &&
            !t.Namespace?.StartsWith("ef") == true &&
            !t.Name.StartsWith("Operation") // Exclude OperationReportHandler, OperationResultHandler     
        )];

        foreach (Type? handler in from handler in handlers
                                  let implementsIRequestHandler = handler.GetInterfaces()
                                      .Any(i => i.IsGenericType && i.Name.Contains("IRequestHandler"))
                                  where !implementsIRequestHandler
                                  select handler)
        {
            PrintWarning(
                $"Handler '{handler.Name}' SHOULD implement 'IRequestHandler'",
                "Integrated Auth Context, Logging, Validation Pipeline, Transaction Support",
                $"{DocsBaseUrl}/backend-guide.md#cqrs-pattern"
            );
        }
    }

    #endregion

    #region Event Validation

    private static void ValidateEvents(List<Type> types)
    {
        // Domain Events
        List<Type> domainEvents = [.. types.Where(t =>
            t.Name.EndsWith("Event") &&
            t is { IsClass: true, IsAbstract: false } &&
            t.Namespace?.Contains("Domain") == true &&
            !typeof(MEntity).IsAssignableFrom(t) // Events shouldn't be entities
        )];

        foreach (Type? evt in from evt in domainEvents
                              let implementsINotification = typeof(Mediator.Mediator.Interfaces.INotification).IsAssignableFrom(evt)
                              where !implementsINotification
                              select evt)
        {
            PrintWarning(
                $"Domain Event '{evt.Name}' SHOULD implement 'INotification'",
                "Event Sourcing Support, MediatR Integration, Automatic Publishing",
                $"{DocsBaseUrl}/backend-guide.md#domain-events"
            );
        }
    }

    #endregion

    #region Background Job Validation

    private static void ValidateBackgroundJobs(List<Type> types)
    {
        List<Type> jobs = [.. types.Where(t =>
            (t.Name.EndsWith("Job") || t.Name.EndsWith("Worker") || t.Name.EndsWith("Task")) &&
            t is { IsClass: true, IsAbstract: false } &&
            !typeof(TenantAwareJobBase).IsAssignableFrom(t) &&
            t.Namespace?.Contains("Jobs") == true
        )];

        foreach (Type? job in jobs)
        {
            PrintWarning(
                $"Background Job '{job.Name}' SHOULD inherit from 'TenantAwareJobBase'",
                "Multi-Tenant Context Propagation, Automatic Logging, Error Handling",
                $"{DocsBaseUrl}/background-jobs-guide.md"
            );
        }
    }

    #endregion

    #region External Service Validation

    private static void ValidateExternalServices(List<Type> types)
    {
        // gRPC Services
        List<Type> grpcServices = [.. types.Where(t =>
            t.Name.EndsWith("GrpcService") &&
            t is { IsClass: true, IsAbstract: false } &&
            !IsSubclassNamed(t, "BaseGrpcService")
        )];

        foreach (Type? svc in grpcServices)
        {
            PrintWarning(
                $"gRPC Service '{svc.Name}' SHOULD inherit from 'BaseGrpcService'",
                "Auth Context Propagation, Interceptor Support, Error Handling",
                $"{DocsBaseUrl}/grpc-guide.md"
            );
        }

        // External API Services
        List<Type> apiServices = [.. types.Where(t =>
            (t.Name.EndsWith("ApiService") || t.Name.EndsWith("Client")) &&
            t is { IsClass: true, IsAbstract: false } &&
            !typeof(BaseApiService).IsAssignableFrom(t) &&
            t.Namespace?.Contains("External") == true
        )];

        foreach (Type? svc in apiServices)
        {
            PrintWarning(
                $"External API Service '{svc.Name}' SHOULD inherit from 'BaseApiService'",
                "Auth Token Forwarding, Resilience Policies, Circuit Breaker",
                $"{DocsBaseUrl}/grpc-guide.md#external-api"
            );
        }
    }

    #endregion

    #region Multi-Tenancy Validation

    private static void ValidateMultiTenancy(List<Type> types)
    {
        // Find entities that have TenantId but don't implement ITenantScoped
        List<Type> entitiesWithTenantId = [.. types.Where(t =>
            t is { IsClass: true, IsAbstract: false } &&
            typeof(MEntity).IsAssignableFrom(t) &&
            t.GetProperty("TenantId") != null &&
            !typeof(ITenantScoped).IsAssignableFrom(t)
        )];

        foreach (Type? entity in entitiesWithTenantId)
        {
            PrintWarning(
                $"Entity '{entity.Name}' has TenantId but SHOULD implement 'ITenantScoped'",
                "Automatic Tenant Filtering in Queries, Tenant Context Injection",
                $"{DocsBaseUrl}/multi-tenant-guide.md"
            );
        }

        // Find entities that look like they need multi-tenancy but don't have TenantId
        List<Type> potentialMultiTenantEntities = [.. types.Where(t =>
            t is { IsClass: true, IsAbstract: false } &&
            typeof(MEntity).IsAssignableFrom(t) &&
            !t.Name.StartsWith('M') && // Skip library entities
            t.GetProperty("TenantId") == null &&
            !typeof(ITenantScoped).IsAssignableFrom(t) &&
            (t.Name.Contains("Order") || t.Name.Contains("Product") || t.Name.Contains("Customer") ||
             t.Name.Contains("Invoice") || t.Name.Contains("Document"))
        )];

        foreach (Type? entity in potentialMultiTenantEntities)
        {
            PrintInfo(
                $"Entity '{entity.Name}' might need Multi-Tenant support",
                "Consider adding 'TenantId' property and implementing 'ITenantScoped'",
                $"{DocsBaseUrl}/multi-tenant-guide.md"
            );
        }
    }

    #endregion

    #region DTO Validation

    private static void ValidateDtos(List<Type> types)
    {
        // Only suggest IMapFrom<T> for DTOs that are clearly response/output models
        // NOT for request models, CQRS commands/queries, or external API models
        List<Type> dtos = [.. types.Where(t =>
                (t.Name.EndsWith("Dto") || t.Name.EndsWith("ViewModel") || t.Name.EndsWith("ResponseModel")) &&
                t is { IsClass: true, IsAbstract: false } &&
                !t.Name.StartsWith('M') && // Skip library DTOs
                // Exclude CQRS requests/commands (they don't need mapping)
                !ImplementsGenericInterface(t, typeof(IRequest<>)) &&
                !typeof(IRequest).IsAssignableFrom(t) &&
                // Exclude request models (input DTOs don't need IMapFrom)
                !t.Name.EndsWith("Request") &&
                !t.Name.EndsWith("RequestModel") &&
                !t.Name.EndsWith("Command") &&
                !t.Name.EndsWith("Query") &&
                // Only suggest for types that have matching entity names
                // e.g., ProductDto should map from Product entity
                types.Any(entity =>
                    typeof(MEntity).IsAssignableFrom(entity) &&
                    t.Name.Replace("Dto", "").Replace("ViewModel", "").Replace("ResponseModel", "")
                        .Equals(entity.Name, StringComparison.OrdinalIgnoreCase))
        )];

        foreach (Type? dto in from dto in dtos
                              let implementsIMapFrom = ImplementsGenericInterface(dto, typeof(IMapFrom<>))
                              where !implementsIMapFrom
                              select dto)
        {
            PrintInfo(
                $"DTO '{dto.Name}' could implement 'IMapFrom<T>' for AutoMapper support",
                "Reduces boilerplate mapping code. Just inject IMapper and call Map<T>.",
                $"{DocsBaseUrl}/backend-guide.md#4-basehandler--irequesthandler"
            );
        }
    }

    #endregion

    #region Dependency Injection Validation

    private static void ValidateDependencyInjection(List<Type> types)
    {
        // Find handlers and services that should inject IMLog
        List<Type> handlersAndServices = [.. types.Where(t =>
            (t.Name.EndsWith("Handler") || t.Name.EndsWith("Service")) &&
            t is { IsClass: true, IsAbstract: false } &&
            !t.Name.StartsWith('M') &&
            !t.Name.StartsWith("Base")
        )];

        foreach (Type? type in handlersAndServices)
        {
            ConstructorInfo[] constructors = type.GetConstructors();
            if (constructors.Length == 0)
            {
                continue;
            }

            ConstructorInfo? mainConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (mainConstructor == null)
            {
                continue;
            }

            ParameterInfo[] parameters = mainConstructor.GetParameters();

            // Check for IMLog injection
            bool hasLogger = parameters.Any(p =>
                p.ParameterType.Name.Contains("IMLog"));

            if (!hasLogger)
            {
                PrintInfo(
                    $"'{type.Name}' should inject 'IMLog<{type.Name}>' for structured logging",
                    "Framework provides automatic correlation ID, tenant context, and request tracking in logs.",
                    $"{DocsBaseUrl}/extensions-log.md"
                );
            }

            // Check for too many dependencies (code smell)
            if (parameters.Length > 7)
            {
                PrintWarning(
                    $"'{type.Name}' has {parameters.Length} constructor dependencies (max recommended: 7)",
                    "Consider splitting into smaller services or using Facade pattern",
                    $"{DocsBaseUrl}/backend-guide.md#dependency-injection"
                );
            }

            // Check handlers that should inject IMapper
            if (type.Name.EndsWith("Handler") &&
                !parameters.Any(p => p.ParameterType.Name.Contains("IMapper")))
            {
                // Only suggest if handler name suggests mapping (Get, List, Query)
                if (type.Name.Contains("Get") || type.Name.Contains("List") || type.Name.Contains("Query"))
                {
                    PrintInfo(
                        $"Query handler '{type.Name}' might benefit from injecting 'IMapper'",
                        "Use 'IMapper' instead of manual mapping.",
                        $"{DocsBaseUrl}/backend-guide.md#4-basehandler--irequesthandler"
                    );
                }
            }
        }
    }

    #endregion

    #region Service Layer Validation

    private static void ValidateServiceLayer(List<Type> types)
    {
        // Find service classes without interfaces (testability concern)
        List<Type> services = [.. types.Where(t =>
            t.Name.EndsWith("Service") &&
            t is { IsClass: true, IsAbstract: false, IsPublic: true } &&
            !t.Name.StartsWith('M') &&
            !t.Name.StartsWith("Base") &&
            !typeof(BaseApiService).IsAssignableFrom(t) &&
            !IsSubclassNamed(t, "BaseGrpcService")
        )];

        foreach (Type? service in services)
        {
            // Check if service has a corresponding interface
            string interfaceName = $"I{service.Name}";
            bool hasInterface = types.Any(t => t.IsInterface && t.Name == interfaceName);

            if (!hasInterface)
            {
                PrintInfo(
                    $"Service '{service.Name}' should have interface '{interfaceName}' for better testability",
                    "Interfaces enable mocking in unit tests and support dependency inversion principle.",
                    $"{DocsBaseUrl}/backend-guide.md#service-layer"
                );
            }
        }

        // Find repositories without UnitOfWork pattern
        List<Type> repositories = [.. types.Where(t =>
            t.Name.EndsWith("Repository") &&
            t is { IsClass: true, IsAbstract: false } &&
            !t.Name.StartsWith('M') &&
            IsSubclassOfRawGeneric(typeof(MRepository<>), t)
        )];

        foreach (Type? repo in repositories)
        {
            ConstructorInfo[] constructors = repo.GetConstructors();
            if (constructors.Length == 0)
            {
                continue;
            }

            ConstructorInfo? mainConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (mainConstructor == null)
            {
                continue;
            }

            bool hasUnitOfWork = mainConstructor.GetParameters()
                .Any(p => p.ParameterType.Name.Contains("IUnitOfWork") || p.ParameterType.Name.Contains("IMUnitOfWork"));

            if (!hasUnitOfWork)
            {
                PrintInfo(
                    $"Repository '{repo.Name}' should inject 'IMUnitOfWork' for transaction management",
                    "UnitOfWork ensures multiple repository operations commit/rollback together atomically.",
                    $"{DocsBaseUrl}/data-layer.md#unit-of-work"
                );
            }
        }
    }

    #endregion

    #region Utilities Reminder

    // Framework utilities reminder has been moved to Muonroi.Docs.
    // See: https://github.com/muonroi/Muonroi.Docs/docs/03-guides/
    private static void PrintFrameworkUtilitiesInfo() { }

    #endregion

    #region Helper Methods

    private static bool IsSubclassOfRawGeneric(Type generic, Type? toCheck)
    {
        while (toCheck != null && toCheck != typeof(object))
        {
            Type cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
            if (generic == cur)
            {
                return true;
            }

            toCheck = toCheck.BaseType;
        }

        return false;
    }

    private static bool ImplementsGenericInterface(Type type, Type genericInterface)
    {
        return type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == genericInterface);
    }

    private static bool IsSubclassNamed(Type type, string baseTypeName)
    {
        Type? current = type.BaseType;
        while (current is not null && current != typeof(object))
        {
            if (string.Equals(current.Name, baseTypeName, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    // Header is suppressed — the deferred startup service logs scan results via IMLog<T>.
    private static void PrintHeader() { }

    private static void PrintWarning(string violation, string benefit, string guide)
    {
        _warningCount++;
        _pendingDiagnostics.Add(("Warning", violation, benefit, guide));
    }

    private static void PrintInfo(string message, string suggestion, string guide)
    {
        _infoCount++;
        _pendingDiagnostics.Add(("Info", message, suggestion, guide));
    }

    private static void PrintSummary(bool throwOnViolation)
    {
        // Summary is emitted by ArchitectureDiagnosticsStartupService on ApplicationStarted.
        if (throwOnViolation && _warningCount > 0)
        {
            throw new MInternalException(
                $"Architecture validation failed with {_warningCount} warning(s). " +
                "Set throwOnViolation=false to run anyway.");
        }
    }

    #endregion

    /// <summary>
    /// Flushes architecture validation diagnostics accumulated during DI registration
    /// to structured logging (IMLog → IMLog) on application startup.
    /// </summary>
    // Marker class used as IMLog<T> category — ArchitectureValidationExtensions is static and
    // cannot be used directly as a type argument.
    private sealed class ArchitectureDiagnosticsCategory { }

    private sealed class ArchitectureDiagnosticsStartupService(IMLog<ArchitectureDiagnosticsCategory> log) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_pendingDiagnostics.Count == 0)
            {
                log.Info("[Architecture] All checks passed for scanned assemblies");
                return Task.CompletedTask;
            }

            foreach ((string level, string message, string supplemental, string guide) in _pendingDiagnostics)
            {
                if (string.Equals(level, "Warning", StringComparison.Ordinal))
                {
                    log.Warn("[Architecture] {Message} | Benefit: {Supplemental} | Guide: {Guide}",
                        message, supplemental, guide);
                }
                else
                {
                    log.Info("[Architecture] {Message} | Suggestion: {Supplemental} | Guide: {Guide}",
                        message, supplemental, guide);
                }
            }

            if (_warningCount > 0)
            {
                log.Warn("[Architecture] Scan complete — {Warnings} warning(s), {Infos} suggestion(s). Review above entries to improve code quality",
                    _warningCount, _infoCount);
            }
            else
            {
                log.Info("[Architecture] Scan complete — {Infos} suggestion(s)", _infoCount);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
