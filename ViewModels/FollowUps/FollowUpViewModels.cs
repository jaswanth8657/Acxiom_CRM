using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels.FollowUps;

public class FollowUpFilterViewModel
{
    public string? SearchTerm { get; set; }
    public FollowUpStatus? Status { get; set; }
    public FollowUpType? Type { get; set; }
    public string? AssignedToUserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class FollowUpListItemViewModel
{
    public int Id { get; set; }
    public int FollowUpId => Id;
    public string Subject { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime FollowUpDate => ScheduledDate;
    public FollowUpType Type { get; set; }
    public FollowUpType FollowUpType => Type;
    public FollowUpStatus Status { get; set; }
    public FollowUpPriority Priority { get; set; }
    public string? Notes { get; set; }
    public string? Remarks => Notes;

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public int? OpportunityId { get; set; }
    public string? OpportunityName { get; set; }

    public string AssignedToUserId { get; set; } = string.Empty;
    public string AssignedTo => AssignedToUserId;
    public string AssignedToUserName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public bool CanEdit { get; set; }
    public bool CanComplete { get; set; }
    public bool CanCancel { get; set; }
    public bool CanMarkMissed { get; set; }

    public bool IsOverdue => (Status == FollowUpStatus.Planned && ScheduledDate.Date < DateTime.Today) || Status == FollowUpStatus.Missed;
    public string? RelatedName => !string.IsNullOrEmpty(CustomerName) ? CustomerName : (!string.IsNullOrEmpty(LeadName) ? LeadName : OpportunityName);
    public string? RelatedType => !string.IsNullOrEmpty(CustomerName) ? "Customer" : (!string.IsNullOrEmpty(LeadName) ? "Lead" : (!string.IsNullOrEmpty(OpportunityName) ? "Opportunity" : null));
}

public class FollowUpListViewModel
{
    public List<FollowUpListItemViewModel> FollowUps { get; set; } = new();
    public FollowUpFilterViewModel Filter { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / (Filter.PageSize > 0 ? Filter.PageSize : 10));
    public int CurrentPage => Filter.Page;
    public int PageSize => Filter.PageSize;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    // Summary Metric Counters
    public int UpcomingCount { get; set; }
    public int TodayCount { get; set; }
    public int OverdueCount { get; set; }
    public int CompletedCount { get; set; }

    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public List<SelectListItem> AvailableStatuses { get; set; } = new();
    public List<SelectListItem> AvailableTypes { get; set; } = new();
    public bool CanManageAll { get; set; }
}

public class FollowUpDetailsViewModel
{
    public int Id { get; set; }
    public int FollowUpId => Id;
    public string Subject { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime FollowUpDate => ScheduledDate;
    public FollowUpType Type { get; set; }
    public FollowUpType FollowUpType => Type;
    public FollowUpStatus Status { get; set; }
    public FollowUpPriority Priority { get; set; }
    public string? Notes { get; set; }
    public string? Remarks => Notes;

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCode { get; set; }

    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public string? LeadCode { get; set; }

    public int? OpportunityId { get; set; }
    public string? OpportunityName { get; set; }

    public string AssignedToUserId { get; set; } = string.Empty;
    public string AssignedTo => AssignedToUserId;
    public string AssignedToUserName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public bool CanEdit { get; set; }
    public bool CanComplete { get; set; }
    public bool CanCancel { get; set; }
    public bool CanMarkMissed { get; set; }
}

public class FollowUpCreateViewModel
{
    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    [Display(Name = "Subject / Agenda")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Follow-up date and time is required.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Scheduled Date & Time")]
    public DateTime ScheduledDate { get; set; } = DateTime.Today.AddHours(10);

    [Display(Name = "Follow-Up Type")]
    public FollowUpType Type { get; set; } = FollowUpType.Call;

    [Display(Name = "Priority")]
    public FollowUpPriority Priority { get; set; } = FollowUpPriority.Medium;

    [Display(Name = "Related Customer (Account)")]
    public int? CustomerId { get; set; }

    [Display(Name = "Related Sales Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Related Opportunity")]
    public int? OpportunityId { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedToUserId { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    [Display(Name = "Remarks / Notes")]
    public string? Notes { get; set; }

    public List<SelectListItem> AvailableCustomers { get; set; } = new();
    public List<SelectListItem> AvailableLeads { get; set; } = new();
    public List<SelectListItem> AvailableOpportunities { get; set; } = new();
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public bool CanAssignAny { get; set; }
}

public class FollowUpEditViewModel
{
    [Required]
    public int Id { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    [Display(Name = "Subject / Agenda")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Scheduled date is required.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Scheduled Date & Time")]
    public DateTime ScheduledDate { get; set; }

    [Display(Name = "Follow-Up Type")]
    public FollowUpType Type { get; set; }

    [Display(Name = "Priority")]
    public FollowUpPriority Priority { get; set; }

    [Display(Name = "Status")]
    public FollowUpStatus Status { get; set; }

    [Display(Name = "Related Customer (Account)")]
    public int? CustomerId { get; set; }

    [Display(Name = "Related Sales Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Related Opportunity")]
    public int? OpportunityId { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedToUserId { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    [Display(Name = "Remarks / Notes")]
    public string? Notes { get; set; }

    [StringLength(500)]
    [Display(Name = "Reschedule / Status Change Reason")]
    public string? ChangeReason { get; set; }

    public List<SelectListItem> AvailableCustomers { get; set; } = new();
    public List<SelectListItem> AvailableLeads { get; set; } = new();
    public List<SelectListItem> AvailableOpportunities { get; set; } = new();
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public bool CanAssignAny { get; set; }
}

public class FollowUpActionViewModel
{
    [Required]
    public int Id { get; set; }

    [StringLength(500)]
    [Display(Name = "Remarks / Notes")]
    public string? Notes { get; set; }

    [Display(Name = "New Scheduled Date (for Reschedule)")]
    public DateTime? RescheduledDate { get; set; }
}
