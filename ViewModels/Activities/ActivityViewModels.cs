using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels.Activities;

public class ActivityFilterViewModel
{
    public string? SearchTerm { get; set; }
    public ActivityType? Type { get; set; }
    public ActivityStatus? Status { get; set; }
    public string? UserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class ActivityListItemViewModel
{
    public int Id { get; set; }
    public int ActivityId => Id;
    public ActivityType Type { get; set; }
    public ActivityType ActivityType => Type;
    public ActivityStatus Status { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ActivityDate { get; set; }

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public int? OpportunityId { get; set; }
    public string? OpportunityName { get; set; }

    public string UserId { get; set; } = string.Empty;
    public string AssignedTo => UserId;
    public string UserName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }

    public string CreatedByName => UserName;
    public string? RelatedName => !string.IsNullOrEmpty(CustomerName) ? CustomerName : (!string.IsNullOrEmpty(LeadName) ? LeadName : OpportunityName);
    public string? RelatedType => !string.IsNullOrEmpty(CustomerName) ? "Customer" : (!string.IsNullOrEmpty(LeadName) ? "Lead" : (!string.IsNullOrEmpty(OpportunityName) ? "Opportunity" : null));
}

public class ActivityListViewModel
{
    public List<ActivityListItemViewModel> Activities { get; set; } = new();
    public ActivityFilterViewModel Filter { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / (Filter.PageSize > 0 ? Filter.PageSize : 10));
    public int CurrentPage => Filter.Page;
    public int PageSize => Filter.PageSize;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public List<SelectListItem> AvailableTypes { get; set; } = new();
    public List<SelectListItem> AvailableStatuses { get; set; } = new();
    public bool CanManageAll { get; set; }
}

public class ActivityDetailsViewModel
{
    public int Id { get; set; }
    public int ActivityId => Id;
    public ActivityType Type { get; set; }
    public ActivityType ActivityType => Type;
    public ActivityStatus Status { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ActivityDate { get; set; }

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public int? OpportunityId { get; set; }
    public string? OpportunityName { get; set; }

    public string UserId { get; set; } = string.Empty;
    public string AssignedTo => UserId;
    public string UserName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class ActivityCreateViewModel
{
    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    [Display(Name = "Subject / Summary")]
    public string Subject { get; set; } = string.Empty;

    [Display(Name = "Activity Type")]
    public ActivityType Type { get; set; } = ActivityType.Call;

    [Display(Name = "Status")]
    public ActivityStatus Status { get; set; } = ActivityStatus.Completed;

    [Required(ErrorMessage = "Activity Date is required.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Activity Date & Time")]
    public DateTime ActivityDate { get; set; } = DateTime.Now;

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    [Display(Name = "Description / Outcome")]
    public string? Description { get; set; }

    [Display(Name = "Related Customer (Account)")]
    public int? CustomerId { get; set; }

    [Display(Name = "Related Sales Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Related Opportunity")]
    public int? OpportunityId { get; set; }

    [Display(Name = "Performed By / Assigned To")]
    public string? UserId { get; set; }

    public List<SelectListItem> AvailableCustomers { get; set; } = new();
    public List<SelectListItem> AvailableLeads { get; set; } = new();
    public List<SelectListItem> AvailableOpportunities { get; set; } = new();
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public bool CanAssignAny { get; set; }
}

public class ActivityEditViewModel
{
    [Required]
    public int Id { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    [Display(Name = "Subject / Summary")]
    public string Subject { get; set; } = string.Empty;

    [Display(Name = "Activity Type")]
    public ActivityType Type { get; set; }

    [Display(Name = "Status")]
    public ActivityStatus Status { get; set; }

    [Required(ErrorMessage = "Activity Date is required.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Activity Date & Time")]
    public DateTime ActivityDate { get; set; }

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    [Display(Name = "Description / Outcome")]
    public string? Description { get; set; }

    [Display(Name = "Related Customer (Account)")]
    public int? CustomerId { get; set; }

    [Display(Name = "Related Sales Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Related Opportunity")]
    public int? OpportunityId { get; set; }

    [Display(Name = "Performed By / Assigned To")]
    public string? UserId { get; set; }

    public List<SelectListItem> AvailableCustomers { get; set; } = new();
    public List<SelectListItem> AvailableLeads { get; set; } = new();
    public List<SelectListItem> AvailableOpportunities { get; set; } = new();
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public bool CanAssignAny { get; set; }
}
