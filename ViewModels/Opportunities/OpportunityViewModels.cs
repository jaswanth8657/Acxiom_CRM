using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels.Opportunities;

public class OpportunityFilterViewModel
{
    public string? SearchTerm { get; set; }
    public OpportunityStage? Stage { get; set; }
    public OpportunityStatus? Status { get; set; }
    public string? AssignedToUserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class OpportunityListItemViewModel
{
    public int Id { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public decimal Amount { get; set; }
    public int Probability { get; set; }
    public decimal WeightedAmount => Math.Round(Amount * Probability / 100m, 2);
    public OpportunityStage Stage { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public OpportunityStatus Status { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class OpportunityListViewModel
{
    public List<OpportunityListItemViewModel> Opportunities { get; set; } = new();
    public OpportunityFilterViewModel Filter { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / (Filter.PageSize > 0 ? Filter.PageSize : 10));
    public int CurrentPage => Filter.Page;
    public int PageSize => Filter.PageSize;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    // Pipeline Summary Metrics
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipeline { get; set; }
    public decimal WeightedPipeline { get; set; }

    // Dropdowns
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public List<SelectListItem> AvailableStages { get; set; } = new();
    public List<SelectListItem> AvailableStatuses { get; set; } = new();
    public bool CanManageAll { get; set; }
}

public class OpportunityCreateViewModel
{
    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
    [Display(Name = "Opportunity Name")]
    public string OpportunityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer is required.")]
    [Display(Name = "Customer Account")]
    public int CustomerId { get; set; }

    [Display(Name = "Related Lead (Optional)")]
    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, 1000000000, ErrorMessage = "Amount must be greater than 0.")]
    [Display(Name = "Amount ($)")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Stage is required.")]
    [Display(Name = "Pipeline Stage")]
    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    [Required(ErrorMessage = "Probability is required.")]
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    [Display(Name = "Win Probability (%)")]
    public int Probability { get; set; } = 25;

    [Required(ErrorMessage = "Expected Close Date is required.")]
    [Display(Name = "Expected Close Date")]
    public DateTime ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);

    [Display(Name = "Assigned Executive")]
    public string? AssignedToUserId { get; set; }

    [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
    [Display(Name = "Notes & Requirements")]
    public string? Notes { get; set; }

    public List<SelectListItem> AvailableCustomers { get; set; } = new();
    public List<SelectListItem> AvailableLeads { get; set; } = new();
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public List<SelectListItem> AvailableStages { get; set; } = new();
}

public class OpportunityEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
    [Display(Name = "Opportunity Name")]
    public string OpportunityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer is required.")]
    [Display(Name = "Customer Account")]
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }

    [Display(Name = "Related Lead (Optional)")]
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, 1000000000, ErrorMessage = "Amount must be greater than 0.")]
    [Display(Name = "Amount ($)")]
    public decimal Amount { get; set; }

    public OpportunityStage CurrentStage { get; set; }

    [Required(ErrorMessage = "Stage is required.")]
    [Display(Name = "Pipeline Stage")]
    public OpportunityStage Stage { get; set; }

    [Required(ErrorMessage = "Probability is required.")]
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    [Display(Name = "Win Probability (%)")]
    public int Probability { get; set; }

    [Required(ErrorMessage = "Expected Close Date is required.")]
    [Display(Name = "Expected Close Date")]
    public DateTime ExpectedCloseDate { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    [Display(Name = "Record Status")]
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Active;

    [Display(Name = "Assigned Executive")]
    public string? AssignedToUserId { get; set; }

    [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
    [Display(Name = "Notes & Requirements")]
    public string? Notes { get; set; }

    [StringLength(500, ErrorMessage = "Stage change note cannot exceed 500 characters.")]
    [Display(Name = "Stage Transition Reason / Note")]
    public string? StageChangeNote { get; set; }

    public bool CanReassign { get; set; } = true;
    public List<SelectListItem> AvailableCustomers { get; set; } = new();
    public List<SelectListItem> AvailableLeads { get; set; } = new();
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public List<SelectListItem> AllowedStages { get; set; } = new();
}

public class OpportunityDetailsViewModel
{
    public int Id { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public string? LeadCode { get; set; }
    public decimal Amount { get; set; }
    public int Probability { get; set; }
    public decimal WeightedAmount => Math.Round(Amount * Probability / 100m, 2);
    public OpportunityStage Stage { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public OpportunityStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public string? Notes { get; set; }

    public List<OpportunityActivityItemViewModel> Activities { get; set; } = new();
    public List<OpportunityFollowUpItemViewModel> FollowUps { get; set; } = new();

    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class OpportunityActivityItemViewModel
{
    public int Id { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ActivityDate { get; set; }
    public string? PerformedByName { get; set; }
}

public class OpportunityFollowUpItemViewModel
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class OpportunityStageChangeViewModel
{
    [Required]
    public int OpportunityId { get; set; }

    [Required]
    public OpportunityStage NewStage { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
