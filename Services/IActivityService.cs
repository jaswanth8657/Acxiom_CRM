using AcxiomCRM.ViewModels.Activities;

namespace AcxiomCRM.Services;

public interface IActivityService
{
    Task<ActivityListViewModel> GetPagedActivitiesAsync(
        ActivityFilterViewModel filter,
        string currentUserId,
        IList<string> roles);

    Task<ActivityDetailsViewModel?> GetActivityDetailsAsync(
        int id,
        string currentUserId,
        IList<string> roles);

    Task<ActivityCreateViewModel> GetActivityCreateViewModelAsync(
        string currentUserId,
        IList<string> roles,
        int? customerId = null,
        int? leadId = null,
        int? opportunityId = null);

    Task<ActivityEditViewModel?> GetActivityForEditAsync(
        int id,
        string currentUserId,
        IList<string> roles);

    Task<(bool Success, string? ErrorMessage, int ActivityId)> CreateActivityAsync(
        ActivityCreateViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress);

    Task<(bool Success, string? ErrorMessage)> UpdateActivityAsync(
        ActivityEditViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress);

    Task<(bool Success, string? ErrorMessage)> CompleteActivityAsync(
        int id,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress);

    Task<(bool Success, string? ErrorMessage)> CancelActivityAsync(
        int id,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress);

    Task<(bool Success, string? ErrorMessage)> DeleteActivityAsync(
        int id,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress);

    Task<bool> CanUserAccessActivityAsync(
        int id,
        string currentUserId,
        IList<string> roles);
}
