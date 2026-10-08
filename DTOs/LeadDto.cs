using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;

namespace AcxiomCRM.DTOs;

public class LeadDto
{
    public int Id { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string? Title { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = "New";
    public decimal? ExpectedValue { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public int? ConvertedCustomerId { get; set; }
    public string? ConvertedCustomerName { get; set; }
    public int? ConvertedOpportunityId { get; set; }
    public DateTime? ConvertedDate { get; set; }
}

public class CreateLeadDto
{
    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
    public string LeadName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(30, ErrorMessage = "Phone cannot exceed 30 characters.")]
    public string? Phone { get; set; }

    [StringLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
    public string? CompanyName { get; set; }

    [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string? Title { get; set; }

    [Required(ErrorMessage = "Source is required.")]
    [StringLength(50, ErrorMessage = "Source cannot exceed 50 characters.")]
    public string Source { get; set; } = "Website";

    [Range(0, 1000000000, ErrorMessage = "Expected Value must be between 0 and 1,000,000,000.")]
    public decimal? ExpectedValue { get; set; }

    [StringLength(50)]
    public string? LeadCode { get; set; }

    public string? AssignedToUserId { get; set; }
}

public class UpdateLeadDto
{
    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
    public string LeadName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(30, ErrorMessage = "Phone cannot exceed 30 characters.")]
    public string? Phone { get; set; }

    [StringLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
    public string? CompanyName { get; set; }

    [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string? Title { get; set; }

    [Required(ErrorMessage = "Source is required.")]
    [StringLength(50, ErrorMessage = "Source cannot exceed 50 characters.")]
    public string Source { get; set; } = "Website";

    [Required]
    public LeadStatus Status { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Expected Value must be between 0 and 1,000,000,000.")]
    public decimal? ExpectedValue { get; set; }

    public string? AssignedToUserId { get; set; }
}

public class ConvertLeadDto
{
    [StringLength(150)]
    public string? CompanyName { get; set; }

    [StringLength(200)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    public bool CreateOpportunity { get; set; } = true;

    [StringLength(150)]
    public string? OpportunityName { get; set; }

    [Range(0.01, 1000000000, ErrorMessage = "Opportunity amount must be greater than 0.")]
    public decimal? OpportunityAmount { get; set; }

    public DateTime? ExpectedCloseDate { get; set; }
}

public class LeadQueryDto
{
    public string? SearchTerm { get; set; }
    public LeadStatus? Status { get; set; }
    public string? Source { get; set; }
    public string? AssignedToUserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
