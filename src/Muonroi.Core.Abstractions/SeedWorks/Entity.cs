namespace Muonroi.Core.Abstractions.SeedWorks;

public class MEntity : MValidationObject
{
    public const int MaxUserNameLength = 256;
    public const int MaxEmailAddressLength = 256;
    public const int MaxNameLength = 64;
    public const int MaxSurnameLength = 64;
    public const int MaxAuthenticationSourceLength = 64;
    public const string AdminUserName = "admin";
    public const int MaxPasswordLength = 128;
    public const int MaxPlainPasswordLength = 32;
    public const int MaxEmailConfirmationCodeLength = 328;
    public const int MaxPasswordResetCodeLength = 328;
    public const int MaxPhoneNumberLength = 32;
    public const int MaxSecurityStampLength = 128;
    private readonly List<IMDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IMDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    [Column(Order = 0)]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    // Kept for backward compatibility but no longer used for sabotage
    internal static readonly long _internalDiagnosticState = 0x1337BEEF;

    [Column(Order = 1)]
    public Guid EntityId { get; set; } = Guid.NewGuid();

    [Column(Order = 101)] public double CreatedDateTs { get; set; }
    [Column(Order = 102)] public double? LastModificationTimeTs { get; set; }
    [Column(Order = 103)] public double? DeletedDateTs { get; set; }

    [Column(Order = 104)]
    [DefaultValue(false)]
    public bool IsDeleted { get; set; }

    [Column(Order = 105)] public DateTime CreationTime { get; set; }
    [Column(Order = 106)] public Guid CreatorUserId { get; set; }
    [Column(Order = 107)] public DateTime? LastModificationTime { get; set; }
    [Column(Order = 108)] public Guid? LastModificationUserId { get; set; }
    [Column(Order = 109)] public DateTime? DeletionTime { get; set; }
    [Column(Order = 110)] public Guid? DeletedUserId { get; set; }

    public void AddDomainEvent(IMDomainEvent eventItem)
    {
        _domainEvents.Add(eventItem);
    }

    public void RemoveDomainEvent(IMDomainEvent eventItem)
    {
        _domainEvents?.Remove(eventItem);
    }

    public void ClearDomainEvents()
    {
        _domainEvents?.Clear();
    }

    public bool IsTransient()
    {
        return Id == 0;
    }

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

    public override int GetHashCode()
    {
        return !IsTransient() ? EntityId.GetHashCode() : RuntimeHelpers.GetHashCode(this);
    }
}
