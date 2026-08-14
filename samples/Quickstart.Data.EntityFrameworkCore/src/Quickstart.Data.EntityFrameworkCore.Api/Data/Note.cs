namespace Quickstart.Data.EntityFrameworkCore.Api.Data;

/// <summary>
/// A minimal sample entity that inherits from <see cref="MEntity"/>.
///
/// By inheriting MEntity it automatically gets:
///   - Id (Snowflake long) and EntityId (Guid)
///   - Audit columns: CreationTime, CreatorUserId, LastModificationTime
///   - Soft-delete: IsDeleted, DeletionTime (DELETE becomes UPDATE IsDeleted=true)
///
/// MDbContext.SaveChangesAsync() stamps the audit fields and converts deletes to
/// soft-deletes via the global query filter on IsDeleted.
/// </summary>
public class Note : MEntity
{
    /// <summary>Gets or sets the note title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the note body.</summary>
    public string Body { get; set; } = string.Empty;
}
