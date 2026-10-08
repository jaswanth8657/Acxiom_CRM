using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class FollowUp
{
    public int Id { get; set; }

    [NotMapped]
    public int FollowUpId
    {
        get => Id;
        set => Id = value;
    }

    public FollowUpType Type { get; set; } = FollowUpType.Call;

    [NotMapped]
    public FollowUpType FollowUpType
    {
        get => Type;
        set => Type = value;
    }

    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public DateTime ScheduledDate { get; set; }

    [NotMapped]
    public DateTime FollowUpDate
    {
        get => ScheduledDate;
        set => ScheduledDate = value;
    }

    public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

    public FollowUpPriority Priority { get; set; } = FollowUpPriority.Medium;

    [StringLength(1000)]
    public string? Notes { get; set; }

    [NotMapped]
    public string? Remarks
    {
        get => Notes;
        set => Notes = value;
    }

    public int? CustomerId { get; set; }
    public virtual Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public virtual Lead? Lead { get; set; }

    public int? OpportunityId { get; set; }
    public virtual Opportunity? Opportunity { get; set; }

    [Required]
    public string AssignedToUserId { get; set; } = string.Empty;
    public virtual ApplicationUser AssignedToUser { get; set; } = null!;

    [NotMapped]
    public string AssignedTo
    {
        get => AssignedToUserId;
        set => AssignedToUserId = value;
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public DateTime CreatedDate
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }

    public DateTime? CompletedAt { get; set; }
}
