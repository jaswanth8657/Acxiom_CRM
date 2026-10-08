using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels.Reports;

// Common pagination base
public abstract class PagedReportBase
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / (PageSize > 0 ? PageSize : 15));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public string? SearchTerm { get; set; }
    public string? AssignedToUserId { get; set; }
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public bool CanFilterUsers { get; set; }
}

// 1. Customer Report
public class CustomerReportViewModel : PagedReportBase
{
    public CustomerStatus? Status { get; set; }
    public List<CustomerReportItem> Items { get; set; } = new();
}

public class CustomerReportItem
{
    public int Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public CustomerStatus Status { get; set; }
    public string AssignedToUserName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

// 2. Lead Report
public class LeadReportViewModel : PagedReportBase
{
    public LeadStatus? Status { get; set; }
    public string? Source { get; set; }
    public List<LeadReportItem> Items { get; set; } = new();
    public List<SelectListItem> AvailableSources { get; set; } = new();
}

public class LeadReportItem
{
    public int Id { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Source { get; set; } = string.Empty;
    public LeadStatus Status { get; set; }
    public decimal ExpectedValue { get; set; }
    public string AssignedToUserName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public int? ConvertedCustomerId { get; set; }
    public string? ConvertedCustomerName { get; set; }
}

// 3. Follow-Up Report
public class FollowUpReportViewModel : PagedReportBase
{
    public FollowUpStatus? Status { get; set; }
    public FollowUpType? Type { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<FollowUpReportItem> Items { get; set; } = new();
}

public class FollowUpReportItem
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public FollowUpType Type { get; set; }
    public FollowUpStatus Status { get; set; }
    public DateTime ScheduledDate { get; set; }
    public bool IsOverdue => (Status == FollowUpStatus.Planned && ScheduledDate.Date < DateTime.Today) || Status == FollowUpStatus.Missed;
    public string? RelatedRecordName { get; set; }
    public string AssignedToUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// 4. Opportunity Report
public class OpportunityReportViewModel : PagedReportBase
{
    public OpportunityStage? Stage { get; set; }
    public OpportunityStatus? Status { get; set; }
    public List<OpportunityReportItem> Items { get; set; } = new();
}

public class OpportunityReportItem
{
    public int Id { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public OpportunityStage Stage { get; set; }
    public decimal Amount { get; set; }
    public int Probability { get; set; }
    public decimal WeightedAmount => Math.Round(Amount * Probability / 100m, 2);
    public DateTime ExpectedCloseDate { get; set; }
    public OpportunityStatus Status { get; set; }
    public string AssignedToUserName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

// 5. Pipeline Report (Stage-wise, Owner-wise, Weighted)
public class PipelineReportViewModel
{
    public decimal TotalPipeline { get; set; }
    public decimal TotalWeightedPipeline { get; set; }
    public int TotalOpenDeals { get; set; }

    public List<StagePipelineSummary> StageBreakdowns { get; set; } = new();
    public List<OwnerPipelineSummary> OwnerBreakdowns { get; set; } = new();
}

public class StagePipelineSummary
{
    public OpportunityStage Stage { get; set; }
    public int DealCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
    public decimal SharePercentage { get; set; }
}

public class OwnerPipelineSummary
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int DealCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
}

// 6. Conversion Report
public class ConversionReportViewModel
{
    public int TotalLeads { get; set; }
    public int ConvertedLeads { get; set; }
    public int OpenLeads { get; set; }
    public int LostLeads { get; set; }
    public decimal OverallConversionRate => TotalLeads > 0 ? Math.Round((decimal)ConvertedLeads / TotalLeads * 100m, 1) : 0;
    public decimal TotalConvertedRevenue { get; set; }
    public decimal AverageConvertedDealValue => ConvertedLeads > 0 ? Math.Round(TotalConvertedRevenue / ConvertedLeads, 2) : 0;

    public List<SourceConversionSummary> SourceBreakdowns { get; set; } = new();
}

public class SourceConversionSummary
{
    public string Source { get; set; } = string.Empty;
    public int TotalLeads { get; set; }
    public int ConvertedLeads { get; set; }
    public int LostLeads { get; set; }
    public decimal ConversionRate => TotalLeads > 0 ? Math.Round((decimal)ConvertedLeads / TotalLeads * 100m, 1) : 0;
}

// 7. User Activity Report
public class UserActivityReportViewModel
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<UserActivitySummaryItem> Summaries { get; set; } = new();
}

public class UserActivitySummaryItem
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int TotalActivities { get; set; }
    public int CallsCount { get; set; }
    public int MeetingsCount { get; set; }
    public int EmailsCount { get; set; }
    public int TasksCount { get; set; }
    public int CompletedFollowUps { get; set; }
    public int WonDealsCount { get; set; }
    public decimal WonDealsValue { get; set; }
}

// 8. Audit Report
public class AuditReportViewModel : PagedReportBase
{
    public string? ActionFilter { get; set; }
    public string? EntityNameFilter { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<AuditReportItem> Items { get; set; } = new();
}

public class AuditReportItem
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; }
}
