namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules;

/// <summary>
/// Lifecycle state of a PDF template version, mirroring <see cref="Muonroi.RuleEngine.Abstractions.Rules.RuleSetStatus"/>.
/// </summary>
public enum PdfTemplateStatus
{
    /// <summary>Draft state — content is editable.</summary>
    Draft = 0,

    /// <summary>Submitted for maker-checker approval.</summary>
    PendingApproval = 1,

    /// <summary>Approved and ready for activation.</summary>
    Approved = 2,

    /// <summary>Rejected by the approver; returns to Draft.</summary>
    Rejected = 3,

    /// <summary>Currently active / published.</summary>
    Active = 4,

    /// <summary>Superseded by a newer active version.</summary>
    Superseded = 5,

    /// <summary>Rolled back after activation.</summary>
    RolledBack = 6
}
