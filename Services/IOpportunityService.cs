using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Opportunities;

namespace AcxiomCRM.Services;

public interface IOpportunityService
{
    Task<OpportunityListViewModel> GetPagedOpportunitiesAsync(OpportunityFilterViewModel filter, string currentUserId, IList<string> roles);
    Task<OpportunityDetailsViewModel?> GetOpportunityDetailsAsync(int id, string currentUserId, IList<string> roles);
    Task<Opportunity?> GetOpportunityByIdAsync(int id);
    Task<bool> CanUserAccessOpportunityAsync(int opportunityId, string currentUserId, IList<string> roles);
    Task<(bool Success, string? Error, int? OpportunityId)> CreateOpportunityAsync(OpportunityCreateViewModel model, string currentUserId, string currentUserName, IList<string> roles, string? ipAddress);
    Task<(bool Success, string? Error)> UpdateOpportunityAsync(OpportunityEditViewModel model, string currentUserId, string currentUserName, IList<string> roles, string? ipAddress);
    Task<(bool Success, string? Error)> DeleteOpportunityAsync(int id, string currentUserId, IList<string> roles, string? ipAddress);
    Task<(bool Success, string? Error)> ChangeStageAsync(int opportunityId, OpportunityStage newStage, string? note, string currentUserId, string currentUserName, IList<string> roles, string? ipAddress);
    bool IsValidStageTransition(OpportunityStage currentStage, OpportunityStage targetStage);
    List<OpportunityStage> GetAllowedNextStages(OpportunityStage currentStage);
    Task<List<SelectListItem>> GetAvailableCustomersAsync(string currentUserId, IList<string> roles);
    Task<List<SelectListItem>> GetAvailableLeadsAsync(string currentUserId, IList<string> roles);
    Task<List<SelectListItem>> GetAvailableAssigneesAsync();
}
