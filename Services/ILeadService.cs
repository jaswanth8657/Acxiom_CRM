using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Leads;

namespace AcxiomCRM.Services;

public interface ILeadService
{
    Task<LeadListViewModel> GetPagedLeadsAsync(LeadFilterViewModel filter, string currentUserId, IList<string> roles);
    Task<LeadDetailsViewModel?> GetLeadDetailsAsync(int id, string currentUserId, IList<string> roles);
    Task<Lead?> GetLeadByIdAsync(int id);
    Task<bool> CanUserAccessLeadAsync(int leadId, string currentUserId, IList<string> roles);
    Task<(bool Success, string? Error, int? LeadId)> CreateLeadAsync(LeadCreateViewModel model, string currentUserId, string currentUserName, string? ipAddress);
    Task<(bool Success, string? Error)> UpdateLeadAsync(LeadEditViewModel model, string currentUserId, string currentUserName, IList<string> roles, string? ipAddress);
    Task<(bool Success, string? Error)> DeleteLeadAsync(int id, string currentUserId, IList<string> roles, string? ipAddress);
    Task<(bool Success, string? Error)> ChangeStatusAsync(int leadId, LeadStatus newStatus, string? note, string currentUserId, string currentUserName, IList<string> roles, string? ipAddress);
    Task<(bool Success, string? Error, int? CustomerId, int? OpportunityId)> ConvertLeadAsync(LeadConvertViewModel model, string currentUserId, string currentUserName, IList<string> roles, string? ipAddress);
    Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null);
    Task<bool> IsLeadCodeUniqueAsync(string leadCode, int? excludeId = null);
    Task<string> GenerateLeadCodeAsync();
    bool IsValidStatusTransition(LeadStatus currentStatus, LeadStatus targetStatus);
    List<LeadStatus> GetAllowedNextStatuses(LeadStatus currentStatus);
    Task<List<SelectListItem>> GetAvailableAssigneesAsync();
}
