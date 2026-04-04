namespace Muonroi.Data.EntityFrameworkCore.Entity;

/// <summary>
/// Base MongoDB entity with common audit fields.
/// </summary>
public abstract class MMongoDbEntity
{
    /// <summary>
    /// Gets the MongoDB document identifier.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonElement("_id")]
    public virtual string? Id { get; protected init; }

    /// <summary>
    /// Gets or sets the creation time in UTC.
    /// </summary>
    [BsonElement("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary

    /// <summary>
    /// Gets or sets the last modification time in UTC.
    /// </summary>
    [BsonElement("lastModifiedDate")]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
}
