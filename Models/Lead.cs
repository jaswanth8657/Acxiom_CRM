using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Lead
{
    public int Id { get; set; }

    [NotMapped]
    public int LeadId => Id;

    [Required(ErrorMessage = "Lead Code is required.")]
    [StringLength(50)]
    public string LeadCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
    public string LeadName { get; set; } = string.Empty;

    // Backward-compatibility properties
    [NotMapped]
    public string FirstName
    {
        get => LeadName.Split(' ', 2)[0];
        set
        {
            if (string.IsNullOrEmpty(LeadName)) LeadName = value;
        }
    }

    [NotMapped]
    public string LastName
    {
        get
        {
            var parts = LeadName.Split(' ', 2);
            return parts.Length > 1 ? parts[1] : string.Empty;
        }
        set
        {
            if (!string.IsNullOrEmpty(value))
                LeadName = $"{FirstName} {value}".Trim();
        }
    }

    [NotMapped]
    public string FullName => LeadName;

    [Required(ErrorMessage = "Enter a valid email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
    public string? CompanyName { get; set; }

    // Backward-compatibility property
    [NotMapped]
    public string? Company
    {
        get => CompanyName;
        set => CompanyName = value;
    }

    [StringLength(100)]
    public string? Title { get; set; }

    [Required(ErrorMessage = "Source is required.")]
    [StringLength(50)]
    public string Source { get; set; } = "Website";

    [Required]
    public LeadStatus Status { get; set; } = LeadStatus.New;

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 1000000000, ErrorMessage = "Expected Value must be between 0 and 1,000,000,000.")]
    public decimal? ExpectedValue { get; set; }

    // Backward-compatibility property
    [NotMapped]
    public decimal? EstimatedValue
    {
        get => ExpectedValue;
        set => ExpectedValue = value;
    }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Backward-compatibility property
    [NotMapped]
    public DateTime CreatedAt
    {
        get => CreatedDate;
        set => CreatedDate = value;
    }

    public DateTime? UpdatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public string? AssignedToUserId { get; set; }
    public virtual ApplicationUser? AssignedToUser { get; set; }

    [NotMapped]
    public string AssignedTo => AssignedToUser?.FullName ?? "Unassigned";

    public int? ConvertedCustomerId { get; set; }
    public virtual Customer? ConvertedCustomer { get; set; }

    public int? ConvertedOpportunityId { get; set; }
    public virtual Opportunity? ConvertedOpportunity { get; set; }

    public DateTime? ConvertedDate { get; set; }

    // Navigation properties
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
}
