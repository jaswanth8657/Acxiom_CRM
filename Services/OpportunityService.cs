using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Opportunities;

namespace AcxiomCRM.Services;

public class OpportunityService : IOpportunityService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<OpportunityService> _logger;

    public OpportunityService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<OpportunityService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<bool> CanUserAccessOpportunityAsync(int opportunityId, string currentUserId, IList<string> roles)
    {
        if (roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager))
        {
            return true;
        }

        var opp = await _context.Opportunities
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == opportunityId);

        if (opp == null)
        {
            return false;
        }

        return opp.AssignedToUserId == currentUserId;
    }

    public async Task<OpportunityListViewModel> GetPagedOpportunitiesAsync(
        OpportunityFilterViewModel filter,
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var query = _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedToUser)
            .AsNoTracking();

        // 1. Role-based Scoping: SalesExecutive sees only assigned opportunities
        if (!isPrivileged)
        {
            query = query.Where(o => o.AssignedToUserId == currentUserId);
        }
        else if (!string.IsNullOrEmpty(filter.AssignedToUserId))
        {
            query = query.Where(o => o.AssignedToUserId == filter.AssignedToUserId);
        }

        // 2. Stage Filter
        if (filter.Stage.HasValue)
        {
            query = query.Where(o => o.Stage == filter.Stage.Value);
        }

        // 3. Status Filter
        if (filter.Status.HasValue)
        {
            query = query.Where(o => o.Status == filter.Status.Value);
        }

        // 4. Search Filter (Opportunity Name, Customer Name, Lead Name)
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(o =>
                o.OpportunityName.ToLower().Contains(term) ||
                (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(term)) ||
                (o.Customer != null && o.Customer.CompanyName != null && o.Customer.CompanyName.ToLower().Contains(term)) ||
                (o.Lead != null && o.Lead.LeadName.ToLower().Contains(term)));
        }

        // 5. Total items count
        var totalItems = await query.CountAsync();

        // 6. Pagination calculation
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var items = await query
            .OrderByDescending(o => o.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OpportunityListItemViewModel
            {
                Id = o.Id,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : "Unknown",
                LeadId = o.LeadId,
                LeadName = o.Lead != null ? o.Lead.LeadName : null,
                Amount = o.Amount,
                Probability = o.Probability,
                Stage = o.Stage,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                AssignedToUserId = o.AssignedToUserId,
                AssignedToUserName = o.AssignedToUser != null ? o.AssignedToUser.FullName : "Unassigned",
                CreatedDate = o.CreatedDate,
                CanEdit = isPrivileged || o.AssignedToUserId == currentUserId,
                CanDelete = isPrivileged || o.AssignedToUserId == currentUserId
            })
            .ToListAsync();

        // 7. Calculate pipeline summary metrics within user scope
        var scopeQuery = _context.Opportunities.AsNoTracking();
        if (!isPrivileged)
        {
            scopeQuery = scopeQuery.Where(o => o.AssignedToUserId == currentUserId);
        }

        var totalOpportunities = await scopeQuery.CountAsync();
        var openOpportunities = await scopeQuery.CountAsync(o => o.Status == OpportunityStatus.Active && o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost);
        var wonOpportunities = await scopeQuery.CountAsync(o => o.Status == OpportunityStatus.Active && o.Stage == OpportunityStage.Won);
        var lostOpportunities = await scopeQuery.CountAsync(o => o.Status == OpportunityStatus.Active && o.Stage == OpportunityStage.Lost);

        var activeOpenDeals = await scopeQuery
            .Where(o => o.Status == OpportunityStatus.Active && o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost)
            .Select(o => new { o.Amount, o.Probability })
            .ToListAsync();

        var totalPipeline = activeOpenDeals.Sum(d => d.Amount);
        var weightedPipeline = activeOpenDeals.Sum(d => Math.Round(d.Amount * d.Probability / 100m, 2));

        // 8. Prepare dropdowns
        var availableUsers = new List<SelectListItem>();
        if (isPrivileged)
        {
            availableUsers = await _userManager.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FullName} ({u.Email})"
                })
                .ToListAsync();
        }

        var availableStages = new[]
        {
            OpportunityStage.Qualification,
            OpportunityStage.Proposal,
            OpportunityStage.Negotiation,
            OpportunityStage.Won,
            OpportunityStage.Lost
        }
        .Select(s => new SelectListItem
        {
            Value = ((int)s).ToString(),
            Text = s.ToString(),
            Selected = filter.Stage.HasValue && filter.Stage.Value == s
        })
        .ToList();

        var availableStatuses = Enum.GetValues<OpportunityStatus>()
            .Select(s => new SelectListItem
            {
                Value = ((int)s).ToString(),
                Text = s.ToString(),
                Selected = filter.Status.HasValue && filter.Status.Value == s
            })
            .ToList();

        return new OpportunityListViewModel
        {
            Opportunities = items,
            Filter = filter,
            TotalItems = totalItems,
            TotalOpportunities = totalOpportunities,
            OpenOpportunities = openOpportunities,
            WonOpportunities = wonOpportunities,
            LostOpportunities = lostOpportunities,
            TotalPipeline = totalPipeline,
            WeightedPipeline = weightedPipeline,
            AvailableUsers = availableUsers,
            AvailableStages = availableStages,
            AvailableStatuses = availableStatuses,
            CanManageAll = isPrivileged
        };
    }

    public async Task<OpportunityDetailsViewModel?> GetOpportunityDetailsAsync(
        int id,
        string currentUserId,
        IList<string> roles)
    {
        var opp = await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedToUser)
            .Include(o => o.Activities.OrderByDescending(a => a.ActivityDate))
                .ThenInclude(a => a.User)
            .Include(o => o.FollowUps.OrderByDescending(f => f.ScheduledDate))
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (opp == null)
        {
            return null;
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && opp.AssignedToUserId != currentUserId)
        {
            return null;
        }

        return new OpportunityDetailsViewModel
        {
            Id = opp.Id,
            OpportunityName = opp.OpportunityName,
            CustomerId = opp.CustomerId,
            CustomerName = opp.Customer != null ? opp.Customer.CustomerName : "Unknown",
            CustomerCode = opp.Customer?.CustomerCode,
            LeadId = opp.LeadId,
            LeadName = opp.Lead?.LeadName,
            LeadCode = opp.Lead?.LeadCode,
            Amount = opp.Amount,
            Probability = opp.Probability,
            Stage = opp.Stage,
            ExpectedCloseDate = opp.ExpectedCloseDate,
            Status = opp.Status,
            CreatedDate = opp.CreatedDate,
            UpdatedAt = opp.UpdatedAt,
            AssignedToUserId = opp.AssignedToUserId,
            AssignedToUserName = opp.AssignedToUser != null ? opp.AssignedToUser.FullName : "Unassigned",
            Notes = opp.Notes,
            CanEdit = isPrivileged || opp.AssignedToUserId == currentUserId,
            CanDelete = isPrivileged || opp.AssignedToUserId == currentUserId,
            Activities = opp.Activities.Select(a => new OpportunityActivityItemViewModel
            {
                Id = a.Id,
                ActivityType = a.Type.ToString(),
                Subject = a.Subject,
                Description = a.Description,
                ActivityDate = a.ActivityDate,
                PerformedByName = a.User?.FullName ?? "System"
            }).ToList(),
            FollowUps = opp.FollowUps.Select(f => new OpportunityFollowUpItemViewModel
            {
                Id = f.Id,
                Subject = f.Subject,
                ScheduledDate = f.ScheduledDate,
                Status = f.Status.ToString(),
                Priority = f.Priority.ToString(),
                Notes = f.Notes
            }).ToList()
        };
    }

    public async Task<Opportunity?> GetOpportunityByIdAsync(int id)
    {
        return await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedToUser)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<(bool Success, string? Error, int? OpportunityId)> CreateOpportunityAsync(
        OpportunityCreateViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        // 1. Business validation: Name
        if (string.IsNullOrWhiteSpace(model.OpportunityName))
        {
            return (false, "Opportunity Name is required.", null);
        }

        // 2. Business validation: Customer existence
        var customerExists = await _context.Customers.AnyAsync(c => c.Id == model.CustomerId);
        if (!customerExists)
        {
            return (false, "Selected Customer does not exist.", null);
        }

        // 3. Business validation: Amount
        if (model.Amount <= 0)
        {
            return (false, "Amount must be greater than 0.", null);
        }

        // 4. Business validation: Probability
        if (model.Probability < 0 || model.Probability > 100)
        {
            return (false, "Probability must be between 0 and 100 inclusive.", null);
        }

        // 5. Business validation: ExpectedCloseDate in the past
        if (model.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            return (false, "Expected Close Date cannot be in the past for an active opportunity.", null);
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        var assignedUser = !isPrivileged ? currentUserId : (!string.IsNullOrWhiteSpace(model.AssignedToUserId) ? model.AssignedToUserId : currentUserId);

        var opportunity = new Opportunity
        {
            OpportunityName = model.OpportunityName.Trim(),
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            Amount = model.Amount,
            Stage = model.Stage,
            Probability = model.Stage == OpportunityStage.Won ? 100 : (model.Stage == OpportunityStage.Lost ? 0 : model.Probability),
            ExpectedCloseDate = model.ExpectedCloseDate,
            Status = OpportunityStatus.Active,
            AssignedToUserId = assignedUser,
            Notes = model.Notes?.Trim(),
            CreatedDate = DateTime.UtcNow
        };

        _context.Opportunities.Add(opportunity);
        await _context.SaveChangesAsync();

        // Initial Activity
        _context.Activities.Add(new Activity
        {
            OpportunityId = opportunity.Id,
            CustomerId = opportunity.CustomerId,
            LeadId = opportunity.LeadId,
            Type = ActivityType.Note,
            Subject = "Opportunity Created",
            Description = $"Opportunity '{opportunity.OpportunityName}' created by {currentUserName}. Amount: {opportunity.Amount:C}, Stage: {opportunity.Stage}, Probability: {opportunity.Probability}%.",
            ActivityDate = DateTime.UtcNow,
            UserId = currentUserId
        });

        // Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "CreateOpportunity",
            EntityName = "Opportunity",
            EntityId = opportunity.Id.ToString(),
            Details = $"Created opportunity '{opportunity.OpportunityName}' (Customer ID: {opportunity.CustomerId}, Amount: {opportunity.Amount:C}, Stage: {opportunity.Stage}, Probability: {opportunity.Probability}%).",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        if (opportunity.Stage == OpportunityStage.Won)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "OpportunityWon",
                EntityName = "Opportunity",
                EntityId = opportunity.Id.ToString(),
                Details = $"Opportunity '{opportunity.OpportunityName}' marked as Won.",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            });
        }
        else if (opportunity.Stage == OpportunityStage.Lost)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "OpportunityLost",
                EntityName = "Opportunity",
                EntityId = opportunity.Id.ToString(),
                Details = $"Opportunity '{opportunity.OpportunityName}' marked as Lost.",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Opportunity {OpportunityId} created by user {UserId}.", opportunity.Id, currentUserId);

        return (true, null, opportunity.Id);
    }

    public async Task<(bool Success, string? Error)> UpdateOpportunityAsync(
        OpportunityEditViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var opp = await _context.Opportunities
            .Include(o => o.AssignedToUser)
            .FirstOrDefaultAsync(o => o.Id == model.Id);

        if (opp == null)
        {
            return (false, "Opportunity not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Cross-user authorization check
        if (!isPrivileged && opp.AssignedToUserId != currentUserId)
        {
            _logger.LogWarning("Forbidden: User {UserId} attempted to update unauthorized Opportunity {OpportunityId}", currentUserId, model.Id);
            return (false, "You are not authorized to update this opportunity.");
        }

        // Business validation: Name
        if (string.IsNullOrWhiteSpace(model.OpportunityName))
        {
            return (false, "Opportunity Name is required.");
        }

        // Business validation: Customer
        var customerExists = await _context.Customers.AnyAsync(c => c.Id == model.CustomerId);
        if (!customerExists)
        {
            return (false, "Selected Customer does not exist.");
        }

        // Business validation: Amount
        if (model.Status == OpportunityStatus.Active && model.Amount <= 0)
        {
            return (false, "Amount must be greater than 0.");
        }

        // Business validation: Probability
        if (model.Probability < 0 || model.Probability > 100)
        {
            return (false, "Probability must be between 0 and 100 inclusive.");
        }

        // Business validation: ExpectedCloseDate in past
        if (model.Status == OpportunityStatus.Active && model.Stage != OpportunityStage.Won && model.Stage != OpportunityStage.Lost && model.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            return (false, "Expected Close Date cannot be in the past for an active opportunity.");
        }

        // Validate Stage Transition
        if (opp.Stage != model.Stage)
        {
            if (!IsValidStageTransition(opp.Stage, model.Stage))
            {
                return (false, $"Invalid stage transition from {opp.Stage} to {model.Stage}.");
            }

            var oldStage = opp.Stage;
            opp.Stage = model.Stage;

            // Audit log for stage change
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "OpportunityStageChange",
                EntityName = "Opportunity",
                EntityId = opp.Id.ToString(),
                Details = $"Stage changed from '{oldStage}' to '{model.Stage}'. Note: {model.StageChangeNote ?? "None"}",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            });

            // Activity for stage change
            _context.Activities.Add(new Activity
            {
                OpportunityId = opp.Id,
                CustomerId = opp.CustomerId,
                LeadId = opp.LeadId,
                Type = ActivityType.Note,
                Subject = $"Stage changed to {model.Stage}",
                Description = $"Pipeline stage advanced from {oldStage} to {model.Stage}. " + (!string.IsNullOrWhiteSpace(model.StageChangeNote) ? $"Note: {model.StageChangeNote}" : string.Empty),
                ActivityDate = DateTime.UtcNow,
                UserId = currentUserId
            });

            // Specific terminal stage logs
            if (model.Stage == OpportunityStage.Won)
            {
                model.Probability = 100;
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = "OpportunityWon",
                    EntityName = "Opportunity",
                    EntityId = opp.Id.ToString(),
                    Details = $"Opportunity '{opp.OpportunityName}' officially closed as Won. Final Amount: {model.Amount:C}.",
                    IpAddress = ipAddress,
                    Timestamp = DateTime.UtcNow
                });
            }
            else if (model.Stage == OpportunityStage.Lost)
            {
                model.Probability = 0;
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = "OpportunityLost",
                    EntityName = "Opportunity",
                    EntityId = opp.Id.ToString(),
                    Details = $"Opportunity '{opp.OpportunityName}' closed as Lost. Rationale: {model.StageChangeNote ?? "None"}.",
                    IpAddress = ipAddress,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        // Assignment Change handling
        if (opp.AssignedToUserId != model.AssignedToUserId)
        {
            if (!isPrivileged)
            {
                return (false, "Only Admins and Managers can reassign opportunities.");
            }

            var oldAssignee = opp.AssignedToUser?.FullName ?? "Unassigned";
            var newAssigneeUser = await _userManager.FindByIdAsync(model.AssignedToUserId ?? "");
            var newAssignee = newAssigneeUser?.FullName ?? "Unassigned";

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "OpportunityAssignmentChange",
                EntityName = "Opportunity",
                EntityId = opp.Id.ToString(),
                Details = $"Opportunity '{opp.OpportunityName}' reassigned from '{oldAssignee}' to '{newAssignee}'.",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            });

            opp.AssignedToUserId = model.AssignedToUserId;
        }

        // Update fields
        opp.OpportunityName = model.OpportunityName.Trim();
        opp.CustomerId = model.CustomerId;
        opp.LeadId = model.LeadId;
        opp.Amount = model.Amount;
        opp.Probability = model.Probability;
        opp.ExpectedCloseDate = model.ExpectedCloseDate;
        opp.Status = model.Status;
        opp.Notes = model.Notes?.Trim();
        opp.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "UpdateOpportunity",
            EntityName = "Opportunity",
            EntityId = opp.Id.ToString(),
            Details = $"Updated opportunity details for '{opp.OpportunityName}'. Amount: {opp.Amount:C}, Probability: {opp.Probability}%, Status: {opp.Status}.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Opportunity {OpportunityId} updated by user {UserId}.", opp.Id, currentUserId);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteOpportunityAsync(
        int id,
        string currentUserId,
        IList<string> roles,
        string? ipAddress)
    {
        var opp = await _context.Opportunities.FirstOrDefaultAsync(o => o.Id == id);
        if (opp == null)
        {
            return (false, "Opportunity not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Security check
        if (!isPrivileged && opp.AssignedToUserId != currentUserId)
        {
            _logger.LogWarning("Forbidden: User {UserId} attempted to delete/deactivate unauthorized Opportunity {OpportunityId}", currentUserId, id);
            return (false, "You are not authorized to delete or deactivate this opportunity.");
        }

        // Soft deactivation to preserve sales history and audit continuity
        opp.Status = OpportunityStatus.Inactive;
        opp.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "DeleteOpportunity",
            EntityName = "Opportunity",
            EntityId = id.ToString(),
            Details = $"Deactivated opportunity '{opp.OpportunityName}' (ID: {opp.Id}). Status set to Inactive.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Opportunity {OpportunityId} deactivated by user {UserId}.", id, currentUserId);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ChangeStageAsync(
        int opportunityId,
        OpportunityStage newStage,
        string? note,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var opp = await _context.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunityId);
        if (opp == null)
        {
            return (false, "Opportunity not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && opp.AssignedToUserId != currentUserId)
        {
            _logger.LogWarning("Forbidden: User {UserId} attempted stage change on unauthorized Opportunity {OpportunityId}", currentUserId, opportunityId);
            return (false, "You are not authorized to modify this opportunity.");
        }

        if (opp.Stage == newStage)
        {
            return (true, null);
        }

        if (!IsValidStageTransition(opp.Stage, newStage))
        {
            return (false, $"Invalid stage transition from {opp.Stage} to {newStage}.");
        }

        var oldStage = opp.Stage;
        opp.Stage = newStage;
        if (newStage == OpportunityStage.Won) opp.Probability = 100;
        if (newStage == OpportunityStage.Lost) opp.Probability = 0;
        opp.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "OpportunityStageChange",
            EntityName = "Opportunity",
            EntityId = opp.Id.ToString(),
            Details = $"Stage changed from {oldStage} to {newStage}. Note: {note ?? "None"}",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        if (newStage == OpportunityStage.Won)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "OpportunityWon",
                EntityName = "Opportunity",
                EntityId = opp.Id.ToString(),
                Details = $"Opportunity '{opp.OpportunityName}' won.",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            });
        }
        else if (newStage == OpportunityStage.Lost)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "OpportunityLost",
                EntityName = "Opportunity",
                EntityId = opp.Id.ToString(),
                Details = $"Opportunity '{opp.OpportunityName}' lost. Note: {note ?? "None"}",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            });
        }

        _context.Activities.Add(new Activity
        {
            OpportunityId = opp.Id,
            CustomerId = opp.CustomerId,
            LeadId = opp.LeadId,
            Type = ActivityType.Note,
            Subject = $"Stage updated to {newStage}",
            Description = $"Stage changed from {oldStage} to {newStage}. " + (!string.IsNullOrWhiteSpace(note) ? $"Note: {note}" : string.Empty),
            ActivityDate = DateTime.UtcNow,
            UserId = currentUserId
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public bool IsValidStageTransition(OpportunityStage currentStage, OpportunityStage targetStage)
    {
        if (currentStage == targetStage) return true;

        // Terminal states cannot be changed
        if (currentStage == OpportunityStage.Won || currentStage == OpportunityStage.Lost)
        {
            return false;
        }

        return (currentStage, targetStage) switch
        {
            (OpportunityStage.Prospecting, OpportunityStage.Qualification) => true,
            (OpportunityStage.Prospecting, OpportunityStage.Proposal) => true,
            (OpportunityStage.Prospecting, OpportunityStage.Negotiation) => true,
            (OpportunityStage.Prospecting, OpportunityStage.Won) => true,
            (OpportunityStage.Prospecting, OpportunityStage.Lost) => true,

            (OpportunityStage.Qualification, OpportunityStage.Proposal) => true,
            (OpportunityStage.Qualification, OpportunityStage.Negotiation) => true,
            (OpportunityStage.Qualification, OpportunityStage.Won) => true,
            (OpportunityStage.Qualification, OpportunityStage.Lost) => true,

            (OpportunityStage.Proposal, OpportunityStage.Negotiation) => true,
            (OpportunityStage.Proposal, OpportunityStage.Won) => true,
            (OpportunityStage.Proposal, OpportunityStage.Lost) => true,
            (OpportunityStage.Proposal, OpportunityStage.Qualification) => true,

            (OpportunityStage.Negotiation, OpportunityStage.Won) => true,
            (OpportunityStage.Negotiation, OpportunityStage.Lost) => true,
            (OpportunityStage.Negotiation, OpportunityStage.Proposal) => true,

            _ => false
        };
    }

    public List<OpportunityStage> GetAllowedNextStages(OpportunityStage currentStage)
    {
        var list = new List<OpportunityStage> { currentStage };
        foreach (OpportunityStage s in Enum.GetValues<OpportunityStage>())
        {
            if (s != currentStage && IsValidStageTransition(currentStage, s))
            {
                list.Add(s);
            }
        }
        return list;
    }

    public async Task<List<SelectListItem>> GetAvailableCustomersAsync(string currentUserId, IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        var query = _context.Customers.AsNoTracking();

        if (!isPrivileged)
        {
            query = query.Where(c => c.AssignedToUserId == currentUserId);
        }

        return await query
            .OrderBy(c => c.CustomerName)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"{c.CustomerName} ({c.CustomerCode})" + (c.CompanyName != null ? $" - {c.CompanyName}" : "")
            })
            .ToListAsync();
    }

    public async Task<List<SelectListItem>> GetAvailableLeadsAsync(string currentUserId, IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        var query = _context.Leads.AsNoTracking();

        if (!isPrivileged)
        {
            query = query.Where(l => l.AssignedToUserId == currentUserId);
        }

        return await query
            .OrderByDescending(l => l.CreatedDate)
            .Select(l => new SelectListItem
            {
                Value = l.Id.ToString(),
                Text = $"{l.LeadName} ({l.LeadCode})" + (l.CompanyName != null ? $" - {l.CompanyName}" : "")
            })
            .ToListAsync();
    }

    public async Task<List<SelectListItem>> GetAvailableAssigneesAsync()
    {
        return await _userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.FullName} ({u.Email})"
            })
            .ToListAsync();
    }
}
