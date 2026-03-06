namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

public class CustomColumnOrderConvention : IModelCustomizer
{
    public void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        foreach ((IMutableEntityType entityType, IMutableProperty idProperty) in from IMutableEntityType entityType in
                     modelBuilder.Model.GetEntityTypes()
                 where typeof(MEntity).IsAssignableFrom(entityType.ClrType)
                 let idProperty = entityType.FindProperty(nameof(MEntity.Id))
                 select (entityType, idProperty))
        {
            idProperty?.SetColumnOrder(0);
            IMutableProperty? entityIdProperty = entityType.FindProperty(nameof(MEntity.EntityId));
            entityIdProperty?.SetColumnOrder(1);
            PropertyInfo[]? baseProperties = entityType.ClrType.BaseType?.GetProperties();
            PropertyInfo[] derivedProperties = entityType.ClrType.GetProperties();
            int order = 2;
            foreach (PropertyInfo prop in derivedProperties)
            {
                IMutableProperty? propertyBuilder = entityType.FindProperty(prop.Name);
                if (propertyBuilder == null ||
                    propertyBuilder.Name == nameof(MEntity.Id) ||
                    propertyBuilder.Name == nameof(MEntity.EntityId)) continue;
                propertyBuilder.SetColumnOrder(order);
                order++;
            }

            if (baseProperties == null) continue;
            foreach (PropertyInfo baseProp in baseProperties)
            {
                IMutableProperty? propertyBuilder = entityType.FindProperty(baseProp.Name);
                if (propertyBuilder == null ||
                    propertyBuilder.Name == nameof(MEntity.Id) ||
                    propertyBuilder.Name == nameof(MEntity.EntityId)) continue;
                propertyBuilder.SetColumnOrder(order);
                order++;
            }
        }
    }
}
