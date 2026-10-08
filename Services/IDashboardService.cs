using AcxiomCRM.ViewModels.Dashboard;

namespace AcxiomCRM.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardMetricsAsync(
        DashboardFilterViewModel filter,
        string currentUserId,
        IList<string> roles);

    Task<LeadStatusChartData> GetLeadStatusChartDataAsync(
        string currentUserId,
        IList<string> roles,
        DateTime? startDate = null,
        DateTime? endDate = null);

    Task<OpportunityPipelineChartData> GetOpportunityPipelineChartDataAsync(
        string currentUserId,
        IList<string> roles,
        DateTime? startDate = null,
        DateTime? endDate = null);

    Task<MonthlySalesChartData> GetMonthlySalesChartDataAsync(
        string currentUserId,
        IList<string> roles);
}
