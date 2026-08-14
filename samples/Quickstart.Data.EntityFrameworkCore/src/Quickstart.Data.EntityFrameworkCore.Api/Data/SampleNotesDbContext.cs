namespace Quickstart.Data.EntityFrameworkCore.Api.Data;

/// <summary>
/// A minimal concrete <see cref="MDbContext"/> for the sample.
///
/// MDbContext is the package's base context — it brings audit timestamping,
/// soft-delete, multi-tenant global query filters, domain-event dispatch, and the
/// built-in Identity DbSets (Users/Roles/Permissions/...). This subclass simply
/// adds one application DbSet (<see cref="Notes"/>).
///
/// The base constructor takes DbContextOptions plus optional collaborators
/// (IMediator, ILicenseGuard, IMLog, IMDateTimeService). All are optional, so the
/// context runs with NO database and NO mediator when paired with the EF Core
/// in-memory provider (see Program.cs). SaveChangesAsync still stamps audit fields
/// and honors the IsDeleted soft-delete filter under the in-memory provider.
/// </summary>
public class SampleNotesDbContext(DbContextOptions<SampleNotesDbContext> options)
    : MDbContext(options)
{
    /// <summary>Gets the notes set.</summary>
    public DbSet<Note> Notes => Set<Note>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // base.OnModelCreating wires audit/soft-delete/tenant filters for every
        // entity, including Note, and configures the Identity model.
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Note>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(256);
        });
    }
}
