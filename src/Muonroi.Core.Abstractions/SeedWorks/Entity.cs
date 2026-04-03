namespace Muonroi.Core.Abstractions.SeedWorks;

/// <summary>
/// Represents a base class for entities in the system.
/// </summary>
public class MEntity : MValidationObject
{
    /// <summary>
    /// Maximum length for a user name.
    /// </summary>
    public const int MaxUserNameLength = 256;

    /// <summary>
    /// Maximum length for an email address.
    /// </summary>
    public const int MaxEmailAddressLength = 256;

    /// <summary>
    /// Maximum length for a name.
    /// </summary>
    public const int MaxNameLength = 64;

    /// <summary>
    /// Maximum length for a surname.
    /// </summary>
    public const int MaxSurnameLength = 64;

    /// <summary>
    /// Maximum length for an authentication source.
    /// </summary>
    public const int MaxAuthenticationSourceLength = 64;

    /// <summary>
    /// Default administrator user name.
    /// </summary>
    public const string AdminUserName = "admin";

    /// <summary>
    /// Maximum length for a password.
    /// </summary>
    public const int MaxPasswordLength = 128;

    /// <summary>
    /// Maximum length for a plain password.
    /// </summary>
    public const int MaxPlainPasswordLength = 32;

    /// <summary>
    /// Maximum length for an email confirmation code.
    /// </summary>
    public const int MaxEmailConfirmationCodeLength = 328;

    /// <summary>
    /// Maximum length for a password reset code.
    /// </summary>
    public const int MaxPasswordResetCodeLength = 328;

    /// <summary>
    /// Maximum length for a phone number.
    /// </summary>
    public const int MaxPhoneNumberLength = 32;

    /// <summary>
    /// Maximum length for a security stamp.
    /// </summary>
    public const int MaxSecurityStampLength = 128;
    private readonly List<IMDomainEvent> _domainEvents = [];

    /// <summary>
    /// Gets the collection of domain events associated with the entity.
    /// </summary>
    public IReadOnlyCollection<IMDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Gets or sets the primary key for the entity.
    /// </summary>
    [Column(Order = 0)]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    // Kept for backward compatibility but no longer used for sabotage
    internal static readonly long _internalDiagnosticState = 0x1337BEEF;

    /// <summary>
    /// Gets or sets a unique identifier for the entity.
    /// </summary>
    [Column(Order = 1)]
    public Guid EntityId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the creation date timestamp.
    /// </summary>
    [Column(Order = 101)] public double CreatedDateTs { get; set; }

    /// <summary>
    /// Gets or sets the last modification date timestamp.
    /// </summary>
    [Column(Order = 102)] public double? LastModificationTimeTs { get; set; }

    /// <summary>
    /// Gets or sets the deletion date timestamp.
    /// </summary>
    [Column(Order = 103)] public double? DeletedDateTs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entity is deleted.
    /// </summary>
    [Column(Order = 104)]
    [DefaultValue(false)]
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    [Column(Order = 105)] public DateTime CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the user identifier who created the entity.
    /// </summary>
    [Column(Order = 106)] public Guid CreatorUserId { get; set; }

    /// <summary>
    /// Gets or sets the last modification time.
    /// </summary>
    [Column(Order = 107)] public DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// Gets or sets the user identifier who last modified the entity.
    /// </summary>
    [Column(Order = 108)] public Guid? LastModificationUserId { get; set; }

    /// <summary>
    /// Gets or sets the deletion time.
    /// </summary>
    [Column(Order = 109)] public DateTime? DeletionTime { get; set; }

    /// <summary>
    /// Gets or sets the user identifier who deleted the entity.
    /// </summary>
    [Column(Order = 110)] public Guid? DeletedUserId { get; set; }

    /// <summary>
    /// Adds a domain event to the entity's collection.
    /// </summary>
    /// <param name="eventItem">The domain event to add.</param>
    public void AddDomainEvent(IMDomainEvent eventItem)
    {
        _domainEvents.Add(eventItem);
    }

    /// <summary>
    /// Removes a domain event from the entity's collection.
    /// </summary>
    /// <param name="eventItem">The domain event to remove.</param>
    public void RemoveDomainEvent(IMDomainEvent eventItem)
    {
        _domainEvents?.Remove(eventItem);
    }

    /// <summary>
    /// Clears all domain events from the entity's collection.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents?.Clear();
    }

    /// <summary>
    /// Checks if the entity is transient (not yet persisted to a database).
    /// </summary>
    /// <returns>True if the entity is transient; otherwise, false.</returns>
    public bool IsTransient()
    {
        return Id == 0;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current entity.
    /// </summary>
    /// <param name="obj">The object to compare with the current entity.</param>
    /// <returns>True if the specified object is equal to the current entity; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not MEntity other)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (GetType() != obj.GetType())
        {
            return false;
        }

        if (IsTransient() || other.IsTransient())
        {
            return false;
        }

        return EntityId.Equals(other.EntityId);
    }

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current entity.</returns>
    public override int GetHashCode()
    {
        return !IsTransient() ? EntityId.GetHashCode() : RuntimeHelpers.GetHashCode(this);
    }
}
