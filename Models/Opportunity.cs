using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Opportunity
{
    public int Id { get; set; }

    [NotMapped]
    public int OpportunityId => Id;

    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
    public string OpportunityName { get; set; } = string.Empty;

    // Backward-compatibility property
    [NotMapped]
    public string Title
    {
        get => OpportunityName;
        set => OpportunityName = value;
    }

    [Required(ErrorMessage = "Customer is required.")]
    public int CustomerId { get; set; }
    public virtual Customer Customer { get; set; } = null!;

    public int? LeadId { get; set; }
    public virtual Lead? Lead { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 1000000000, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Stage is required.")]
    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 10;

    [NotMapped]
    public decimal WeightedAmount => Math.Round(Amount * Probability / 100m, 2);

    [Required(ErrorMessage = "Expected Close Date is required.")]
    public DateTime ExpectedCloseDate { get; set; }

    public OpportunityStatus Status { get; set; } = OpportunityStatus.Active;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Backward-compatibility property
    [NotMapped]
    public DateTime CreatedAt
    {
        get => CreatedDate;
        set => CreatedDate = value;
    }

    public DateTime? UpdatedAt { get; set; }

    public string? AssignedToUserId { get; set; }
    public virtual ApplicationUser? AssignedToUser { get; set; }

    [NotMapped]
    public string AssignedTo => AssignedToUser?.FullName ?? "Unassigned";

    [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
}
