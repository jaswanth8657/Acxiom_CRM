using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels.Leads;

public class LeadFilterViewModel
{
    public string? SearchTerm { get; set; }
    public LeadStatus? Status { get; set; }
    public string? Source { get; set; }
    public string? AssignedToUserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class LeadListItemViewModel
{
    public int Id { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string Source { get; set; } = string.Empty;
    public LeadStatus Status { get; set; }
    public decimal? ExpectedValue { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanConvert { get; set; }
}

public class LeadListViewModel
{
    public List<LeadListItemViewModel> Leads { get; set; } = new();
    public LeadFilterViewModel Filter { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / (Filter.PageSize > 0 ? Filter.PageSize : 10));
    public int CurrentPage => Filter.Page;
    public int PageSize => Filter.PageSize;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    // Summary counts
    public int TotalCount { get; set; }
    public int NewCount { get; set; }
    public int ContactedCount { get; set; }
    public int QualifiedCount { get; set; }
    public int ConvertedCount { get; set; }
    public int LostCount { get; set; }

    // Dropdowns
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public List<SelectListItem> AvailableSources { get; set; } = new();
    public List<SelectListItem> AvailableStatuses { get; set; } = new();
    public bool CanManageAll { get; set; }
}

public class LeadCreateViewModel
{
    [Display(Name = "Lead Code")]
    [StringLength(50)]
    public string? LeadCode { get; set; }

    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a valid email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [RegularExpression(@"^[\+]?([0-9]{1,3}[\s.-]?)?[(]?[0-9]{2,4}[)]?[\s.-]?[0-9]{3,4}[\s.-]?[0-9]{3,6}$", ErrorMessage = "Enter a valid phone number.")]
    [StringLength(30, ErrorMessage = "Phone cannot exceed 30 characters.")]
    public string? Phone { get; set; }

    [StringLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string? Title { get; set; }

    [Required(ErrorMessage = "Source is required.")]
    [StringLength(50, ErrorMessage = "Source cannot exceed 50 characters.")]
    public string Source { get; set; } = "Website";

    [Required(ErrorMessage = "Status is required.")]
    public LeadStatus Status { get; set; } = LeadStatus.New;

    [Range(0, 1000000000, ErrorMessage = "Expected Value must be between 0 and 1,000,000,000.")]
    [Display(Name = "Expected Value ($)")]
    public decimal? ExpectedValue { get; set; }

    [Display(Name = "Assign To")]
    public string? AssignedToUserId { get; set; }

    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public List<SelectListItem> AvailableSources { get; set; } = new();
}

public class LeadEditViewModel
{
    public int Id { get; set; }

    [Display(Name = "Lead Code")]
    public string LeadCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a valid email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [RegularExpression(@"^[\+]?([0-9]{1,3}[\s.-]?)?[(]?[0-9]{2,4}[)]?[\s.-]?[0-9]{3,4}[\s.-]?[0-9]{3,6}$", ErrorMessage = "Enter a valid phone number.")]
    [StringLength(30, ErrorMessage = "Phone cannot exceed 30 characters.")]
    public string? Phone { get; set; }

    [StringLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string? Title { get; set; }

    [Required(ErrorMessage = "Source is required.")]
    [StringLength(50, ErrorMessage = "Source cannot exceed 50 characters.")]
    public string Source { get; set; } = "Website";

    public LeadStatus CurrentStatus { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public LeadStatus Status { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Expected Value must be between 0 and 1,000,000,000.")]
    [Display(Name = "Expected Value ($)")]
    public decimal? ExpectedValue { get; set; }

    [Display(Name = "Assign To")]
    public string? AssignedToUserId { get; set; }

    [Display(Name = "Status Change Note")]
    [StringLength(500, ErrorMessage = "Note cannot exceed 500 characters.")]
    public string? StatusChangeNote { get; set; }

    public bool CanReassign { get; set; } = true;
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public List<SelectListItem> AvailableSources { get; set; } = new();
    public List<SelectListItem> AllowedStatuses { get; set; } = new();
}

public class LeadDetailsViewModel
{
    public int Id { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string? Title { get; set; }
    public string Source { get; set; } = string.Empty;
    public LeadStatus Status { get; set; }
    public decimal? ExpectedValue { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }

    // Converted Details
    public int? ConvertedCustomerId { get; set; }
    public string? ConvertedCustomerName { get; set; }
    public int? ConvertedOpportunityId { get; set; }
    public string? ConvertedOpportunityTitle { get; set; }
    public DateTime? ConvertedDate { get; set; }

    public List<LeadActivityItemViewModel> Activities { get; set; } = new();
    public List<LeadFollowUpItemViewModel> FollowUps { get; set; } = new();

    public bool CanEdit { get; set; }
    public bool CanConvert { get; set; }
    public bool CanDelete { get; set; }
}

public class LeadActivityItemViewModel
{
    public int Id { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ActivityDate { get; set; }
    public string? PerformedByName { get; set; }
}

public class LeadFollowUpItemViewModel
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class LeadConvertViewModel
{
    public int LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public decimal? ExpectedValue { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }

    // Existing customer duplicate detection
    public bool ExistingCustomerFound { get; set; }
    public int? ExistingCustomerId { get; set; }
    public string? ExistingCustomerName { get; set; }
    public string? ExistingCustomerCode { get; set; }

    // Opportunity creation
    [Required(ErrorMessage = "Opportunity Title is required.")]
    [StringLength(150, ErrorMessage = "Opportunity Title cannot exceed 150 characters.")]
    [Display(Name = "Opportunity Title")]
    public string OpportunityTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Opportunity Amount is required.")]
    [Range(0, 1000000000, ErrorMessage = "Amount must be between 0 and 1,000,000,000.")]
    [Display(Name = "Opportunity Amount ($)")]
    public decimal? OpportunityAmount { get; set; }

    [Required(ErrorMessage = "Expected Close Date is required.")]
    [Display(Name = "Expected Close Date")]
    public DateTime? ExpectedCloseDate { get; set; }

    [Display(Name = "Initial Stage")]
    public OpportunityStage OpportunityStage { get; set; } = OpportunityStage.Prospecting;

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }
}

public class LeadStatusChangeViewModel
{
    [Required]
    public int LeadId { get; set; }

    [Required]
    public LeadStatus NewStatus { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
