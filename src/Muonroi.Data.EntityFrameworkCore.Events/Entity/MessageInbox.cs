using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Muonroi.Data.EntityFrameworkCore.Entity;

/// <summary>
/// Represents an inbox entry for message deduplication.
/// </summary>
[Table("MessageInbox", Schema = "shared")]
public class MessageInbox
{
    /// <summary>
    /// Gets or sets the unique message identifier.
    /// </summary>
    [Key]
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the consumer name that processed the message.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ConsumerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the message was processed.
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
