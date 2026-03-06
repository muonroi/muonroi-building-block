namespace Muonroi.AspNetCore.Controllers;

public class GenericControllerFeatureProvider(IEnumerable<Assembly> assemblies) : IApplicationFeatureProvider<ControllerFeature>
{
    public GenericControllerFeatureProvider() : this([Assembly.GetEntryAssembly()!]) { }

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        Assembly? currentAssembly = Assembly.GetEntryAssembly();
        if (currentAssembly == null)
        {
            return;
        }

        // Find the actual DbContext type from the application (could be AppDbContext : MDbContext)
        // Scan all provided assemblies for a DbContext
        Type? dbContextType = null;
        foreach (Assembly asm in assemblies)
        {
            dbContextType = asm.GetExportedTypes()
                .FirstOrDefault(x => x.IsSubclassOf(typeof(MDbContext)) && !x.IsAbstract);
            if (dbContextType != null)
            {
                break;
            }
        }

        // Fallback to EntryAssembly or Base MDbContext
        dbContextType ??= currentAssembly.GetExportedTypes()
                              .FirstOrDefault(x => x.IsSubclassOf(typeof(MDbContext)) && !x.IsAbstract)
                          ?? typeof(MDbContext);

        foreach (Assembly assembly in assemblies)
        {
            // Find all entities that inherit from MEntity
            IEnumerable<Type> candidates = assembly.GetExportedTypes()
                .Where(x => x.IsSubclassOf(typeof(MEntity)) && !x.IsAbstract);

            foreach (Type? candidate in candidates)
            {
                feature.Controllers.Add(
                    typeof(MGenericController<,>).MakeGenericType(candidate, dbContextType).GetTypeInfo()
                );
            }
        }
    }
}

