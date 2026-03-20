namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig
{
    /// <summary>
    /// Configures the <see cref="MPermission"/> entity model.
    /// </summary>
    public class MPermissionConfiguration : IEntityTypeConfiguration<MPermission>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<MPermission> builder)
        {
            builder.HasIndex(b => b.Name)
                .HasDatabaseName("IX_MPermissions_Name")
                .IsUnique();

            builder.HasIndex(b => new { b.PermissionGroupId, b.UiKey })
                .HasDatabaseName("IX_MPermissions_Group_UiKey")
                .IsUnique();

            builder.HasOne(p => p.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(p => p.ParentId)
                .HasPrincipalKey(p => p.EntityId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(b => b.Type).HasMaxLength(MPermission.MaxTypeLength);
            builder.Property(b => b.UiKey).HasMaxLength(MPermission.MaxUiKeyLength).IsRequired();
            builder.Property(b => b.ParentUiKey).HasMaxLength(MPermission.MaxParentUiKeyLength);
            builder.Property(b => b.Label).HasMaxLength(MPermission.MaxLabelLength);
            builder.Property(b => b.Icon).HasMaxLength(MPermission.MaxIconLength);
            builder.Property(b => b.Description).HasMaxLength(MPermission.MaxDescriptionLength);
        }
    }
}
