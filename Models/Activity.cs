using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Activity
{
    public int Id { get; set; }

    [NotMapped]
    public int ActivityId
    {
        get => Id;
        set => Id = value;
    }

    public ActivityType Type { get; set; } = ActivityType.Task;

    [NotMapped]
    public ActivityType ActivityType
    {
        get => Type;
        set => Type = value;
    }

    public ActivityStatus Status { get; set; } = ActivityStatus.Completed;

    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateTime ActivityDate { get; set; }

    public int? CustomerId { get; set; }
    public virtual Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public virtual Lead? Lead { get; set; }

    public int? OpportunityId { get; set; }
    public virtual Opportunity? Opportunity { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;

    [NotMapped]
    public string AssignedTo
    {
        get => UserId;
        set => UserId = value;
    }

    [NotMapped]
    public string AssignedToUserId
    {
        get => UserId;
        set => UserId = value;
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
