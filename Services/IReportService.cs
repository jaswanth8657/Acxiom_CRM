using AcxiomCRM.ViewModels.Reports;

namespace AcxiomCRM.Services;

public interface IReportService
{
    Task<CustomerReportViewModel> GetCustomerReportAsync(
        CustomerReportViewModel filter,
        string currentUserId,
        IList<string> roles);

    Task<LeadReportViewModel> GetLeadReportAsync(
        LeadReportViewModel filter,
        string currentUserId,
        IList<string> roles);

    Task<FollowUpReportViewModel> GetFollowUpReportAsync(
        FollowUpReportViewModel filter,
        string currentUserId,
        IList<string> roles);

    Task<OpportunityReportViewModel> GetOpportunityReportAsync(
        OpportunityReportViewModel filter,
        string currentUserId,
        IList<string> roles);

    Task<PipelineReportViewModel> GetPipelineReportAsync(
        string currentUserId,
        IList<string> roles);

    Task<ConversionReportViewModel> GetConversionReportAsync(
        string currentUserId,
        IList<string> roles);

    Task<UserActivityReportViewModel> GetUserActivityReportAsync(
        DateTime? startDate,
        DateTime? endDate,
        string currentUserId,
        IList<string> roles);

    Task<AuditReportViewModel> GetAuditReportAsync(
        AuditReportViewModel filter,
        string currentUserId,
        IList<string> roles);

    Task<byte[]> ExportCustomerReportCsvAsync(CustomerReportViewModel filter, string currentUserId, IList<string> roles);
    Task<byte[]> ExportLeadReportCsvAsync(LeadReportViewModel filter, string currentUserId, IList<string> roles);
    Task<byte[]> ExportFollowUpReportCsvAsync(FollowUpReportViewModel filter, string currentUserId, IList<string> roles);
    Task<byte[]> ExportOpportunityReportCsvAsync(OpportunityReportViewModel filter, string currentUserId, IList<string> roles);
    Task<byte[]> ExportPipelineReportCsvAsync(string currentUserId, IList<string> roles);
    Task<byte[]> ExportConversionReportCsvAsync(string currentUserId, IList<string> roles);
    Task<byte[]> ExportUserActivityReportCsvAsync(DateTime? startDate, DateTime? endDate, string currentUserId, IList<string> roles);
    Task<byte[]> ExportAuditReportCsvAsync(AuditReportViewModel filter, string currentUserId, IList<string> roles);
}
