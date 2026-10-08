using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Activities;
using AcxiomCRM.ViewModels.FollowUps;

namespace AcxiomCRM.ViewModels.Dashboard;

public enum DateFilterPeriod
{
    AllTime = 0,
    Today = 1,
    ThisWeek = 2,
    ThisMonth = 3,
    Custom = 4
}

public class DashboardFilterViewModel
{
    public DateFilterPeriod Period { get; set; } = DateFilterPeriod.AllTime;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class DashboardViewModel
{
    public string CurrentUserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public DashboardFilterViewModel Filter { get; set; } = new();

    // 11 Core KPI Metrics
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int PendingFollowUps { get; set; }
    public int OverdueFollowUps { get; set; }

    // Chart.js Data Sets (Serialisable to JSON)
    public LeadStatusChartData LeadStatusChart { get; set; } = new();
    public OpportunityPipelineChartData OpportunityPipelineChart { get; set; } = new();
    public MonthlySalesChartData MonthlySalesChart { get; set; } = new();

    // Recent Lists
    public List<FollowUpListItemViewModel> RecentFollowUps { get; set; } = new();
    public List<ActivityListItemViewModel> RecentActivities { get; set; } = new();
}

public class LeadStatusChartData
{
    public List<string> Labels { get; set; } = new() { "New", "Contacted", "Qualified", "Lost", "Converted" };
    public List<int> Counts { get; set; } = new();
}

public class OpportunityPipelineChartData
{
    public List<string> Labels { get; set; } = new() { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
    public List<int> Counts { get; set; } = new();
    public List<decimal> Amounts { get; set; } = new();
}

public class MonthlySalesChartData
{
    public List<string> Months { get; set; } = new();
    public List<decimal> WonAmounts { get; set; } = new();
    public List<int> WonCounts { get; set; } = new();
}
