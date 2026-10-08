using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Customer
{
    public int Id { get; set; }

    [NotMapped]
    public int CustomerId => Id;

    [Required(ErrorMessage = "Customer Code is required.")]
    [StringLength(50)]
    public string CustomerCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(100, ErrorMessage = "Customer Name cannot exceed 100 characters.")]
    public string CustomerName { get; set; } = string.Empty;

    // Backward-compatibility property
    [NotMapped]
    public string ContactName
    {
        get => CustomerName;
        set => CustomerName = value;
    }

    [Required(ErrorMessage = "Enter a valid email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    public string? CompanyName { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    [StringLength(100)]
    public string? Industry { get; set; }

    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

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

    // Navigation properties
    public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
}
