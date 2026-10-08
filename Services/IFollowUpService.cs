using AcxiomCRM.ViewModels.FollowUps;

namespace AcxiomCRM.Services;

public interface IFollowUpService
{
    Task<FollowUpListViewModel> GetPagedFollowUpsAsync(
        FollowUpFilterViewModel filter,
        string currentUserId,
        IList<string> roles);

    Task<FollowUpDetailsViewModel?> GetFollowUpDetailsAsync(
        int id,
        string currentUserId,
        IList<string> roles);

    Task<FollowUpEditViewModel?> GetFollowUpForEditAsync(
        int id,
        string currentUserId,
        IList<string> roles);

    Task<FollowUpCreateViewModel> GetFollowUpCreateViewModelAsync(
        string currentUserId,
        IList<string> roles,
        int? customerId = null,
        int? leadId = null,
        int? opportunityId = null);

    Task<(bool Success, string? ErrorMessage, int FollowUpId)> CreateFollowUpAsync(
        FollowUpCreateViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress);

    Task<(bool Success, string? ErrorMessage)> UpdateFollowUpAsync(
        FollowUpEditViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress);

    Task<(bool Success, string? ErrorMessage)> CompleteFollowUpAsync(
        int id,
        string? notes,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress);

    Task<(bool Success, string? ErrorMessage)> MarkMissedFollowUpAsync(
        int id,
        string? reason,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress);

    Task<(bool Success, string? ErrorMessage)> CancelFollowUpAsync(
        int id,
        string? reason,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress);

    Task<(bool Success, string? ErrorMessage)> RescheduleFollowUpAsync(
        int id,
        DateTime newDate,
        string? reason,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress);

    Task<bool> CanUserAccessFollowUpAsync(
        int id,
        string currentUserId,
        IList<string> roles);
}
