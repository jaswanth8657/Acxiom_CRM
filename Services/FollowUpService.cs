using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.FollowUps;

namespace AcxiomCRM.Services;

public class FollowUpService : IFollowUpService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public FollowUpService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<bool> CanUserAccessFollowUpAsync(
        int id,
        string currentUserId,
        IList<string> roles)
    {
        if (roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager))
        {
            return true;
        }

        var followUp = await _context.FollowUps
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (followUp == null)
        {
            return false;
        }

        return followUp.AssignedToUserId == currentUserId;
    }

    public async Task<FollowUpListViewModel> GetPagedFollowUpsAsync(
        FollowUpFilterViewModel filter,
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.Opportunity)
            .Include(f => f.AssignedToUser)
            .AsNoTracking();

        // 1. Role Scoping
        if (!isPrivileged)
        {
            query = query.Where(f => f.AssignedToUserId == currentUserId);
        }
        else if (!string.IsNullOrEmpty(filter.AssignedToUserId))
        {
            query = query.Where(f => f.AssignedToUserId == filter.AssignedToUserId);
        }

        // 2. Status Filter
        if (filter.Status.HasValue)
        {
            query = query.Where(f => f.Status == filter.Status.Value);
        }

        // 3. Type Filter
        if (filter.Type.HasValue)
        {
            query = query.Where(f => f.Type == filter.Type.Value);
        }

        // 4. Date Range Filter
        if (filter.StartDate.HasValue)
        {
            var start = filter.StartDate.Value.Date;
            query = query.Where(f => f.ScheduledDate >= start);
        }
        if (filter.EndDate.HasValue)
        {
            var end = filter.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(f => f.ScheduledDate <= end);
        }

        // 5. Search Filter
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(f =>
                f.Subject.ToLower().Contains(term) ||
                (f.Notes != null && f.Notes.ToLower().Contains(term)) ||
                (f.Customer != null && f.Customer.CustomerName.ToLower().Contains(term)) ||
                (f.Lead != null && f.Lead.LeadName.ToLower().Contains(term)) ||
                (f.Opportunity != null && f.Opportunity.OpportunityName.ToLower().Contains(term)));
        }

        // 6. Pagination
        var totalItems = await query.CountAsync();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var items = await query
            .OrderByDescending(f => f.ScheduledDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FollowUpListItemViewModel
            {
                Id = f.Id,
                Subject = f.Subject,
                ScheduledDate = f.ScheduledDate,
                Type = f.Type,
                Status = f.Status,
                Priority = f.Priority,
                Notes = f.Notes,
                CustomerId = f.CustomerId,
                CustomerName = f.Customer != null ? f.Customer.CustomerName : null,
                LeadId = f.LeadId,
                LeadName = f.Lead != null ? f.Lead.LeadName : null,
                OpportunityId = f.OpportunityId,
                OpportunityName = f.Opportunity != null ? f.Opportunity.OpportunityName : null,
                AssignedToUserId = f.AssignedToUserId,
                AssignedToUserName = f.AssignedToUser != null ? f.AssignedToUser.FullName : "Unassigned",
                CreatedAt = f.CreatedAt,
                CompletedAt = f.CompletedAt,
                CanEdit = isPrivileged || f.AssignedToUserId == currentUserId,
                CanComplete = (isPrivileged || f.AssignedToUserId == currentUserId) && f.Status != FollowUpStatus.Completed && f.Status != FollowUpStatus.Cancelled,
                CanCancel = (isPrivileged || f.AssignedToUserId == currentUserId) && f.Status != FollowUpStatus.Cancelled && f.Status != FollowUpStatus.Completed,
                CanMarkMissed = (isPrivileged || f.AssignedToUserId == currentUserId) && f.Status == FollowUpStatus.Planned
            })
            .ToListAsync();

        // 7. Calculate Scope-based Summary Metrics
        var scopeQuery = _context.FollowUps.AsNoTracking();
        if (!isPrivileged)
        {
            scopeQuery = scopeQuery.Where(f => f.AssignedToUserId == currentUserId);
        }

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var upcomingCount = await scopeQuery.CountAsync(f => f.Status == FollowUpStatus.Planned && f.ScheduledDate >= tomorrow);
        var todayCount = await scopeQuery.CountAsync(f => f.Status == FollowUpStatus.Planned && f.ScheduledDate >= today && f.ScheduledDate < tomorrow);
        var overdueCount = await scopeQuery.CountAsync(f => (f.Status == FollowUpStatus.Planned && f.ScheduledDate < today) || f.Status == FollowUpStatus.Missed);
        var completedCount = await scopeQuery.CountAsync(f => f.Status == FollowUpStatus.Completed);

        // 8. Dropdown Options
        var availableUsers = new List<SelectListItem>();
        if (isPrivileged)
        {
            var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
            availableUsers = users.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.FullName} ({u.Email})",
                Selected = u.Id == filter.AssignedToUserId
            }).ToList();
        }

        var availableStatuses = Enum.GetValues<FollowUpStatus>()
            .DistinctBy(s => (int)s)
            .Select(s => new SelectListItem
            {
                Value = s.ToString(),
                Text = s.ToString(),
                Selected = filter.Status.HasValue && filter.Status.Value == s
            }).ToList();

        var availableTypes = Enum.GetValues<FollowUpType>()
            .Select(t => new SelectListItem
            {
                Value = t.ToString(),
                Text = t.ToString(),
                Selected = filter.Type.HasValue && filter.Type.Value == t
            }).ToList();

        return new FollowUpListViewModel
        {
            FollowUps = items,
            Filter = filter,
            TotalItems = totalItems,
            UpcomingCount = upcomingCount,
            TodayCount = todayCount,
            OverdueCount = overdueCount,
            CompletedCount = completedCount,
            AvailableUsers = availableUsers,
            AvailableStatuses = availableStatuses,
            AvailableTypes = availableTypes,
            CanManageAll = isPrivileged
        };
    }

    public async Task<FollowUpDetailsViewModel?> GetFollowUpDetailsAsync(
        int id,
        string currentUserId,
        IList<string> roles)
    {
        var followUp = await _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.Opportunity)
            .Include(f => f.AssignedToUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (followUp == null)
        {
            return null;
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && followUp.AssignedToUserId != currentUserId)
        {
            return null;
        }

        return new FollowUpDetailsViewModel
        {
            Id = followUp.Id,
            Subject = followUp.Subject,
            ScheduledDate = followUp.ScheduledDate,
            Type = followUp.Type,
            Status = followUp.Status,
            Priority = followUp.Priority,
            Notes = followUp.Notes,
            CustomerId = followUp.CustomerId,
            CustomerName = followUp.Customer?.CustomerName,
            CustomerCode = followUp.Customer?.CustomerCode,
            LeadId = followUp.LeadId,
            LeadName = followUp.Lead?.LeadName,
            LeadCode = followUp.Lead?.LeadCode,
            OpportunityId = followUp.OpportunityId,
            OpportunityName = followUp.Opportunity?.OpportunityName,
            AssignedToUserId = followUp.AssignedToUserId,
            AssignedToUserName = followUp.AssignedToUser != null ? followUp.AssignedToUser.FullName : "Unassigned",
            CreatedAt = followUp.CreatedAt,
            CompletedAt = followUp.CompletedAt,
            CanEdit = isPrivileged || followUp.AssignedToUserId == currentUserId,
            CanComplete = (isPrivileged || followUp.AssignedToUserId == currentUserId) && followUp.Status != FollowUpStatus.Completed && followUp.Status != FollowUpStatus.Cancelled,
            CanCancel = (isPrivileged || followUp.AssignedToUserId == currentUserId) && followUp.Status != FollowUpStatus.Cancelled && followUp.Status != FollowUpStatus.Completed,
            CanMarkMissed = (isPrivileged || followUp.AssignedToUserId == currentUserId) && followUp.Status == FollowUpStatus.Planned
        };
    }

    public async Task<FollowUpCreateViewModel> GetFollowUpCreateViewModelAsync(
        string currentUserId,
        IList<string> roles,
        int? customerId = null,
        int? leadId = null,
        int? opportunityId = null)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var vm = new FollowUpCreateViewModel
        {
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId,
            ScheduledDate = DateTime.Today.AddHours(14),
            CanAssignAny = isPrivileged,
            AssignedToUserId = isPrivileged ? currentUserId : currentUserId
        };

        await PopulateCreateDropdownsAsync(vm, currentUserId, isPrivileged);
        return vm;
    }

    public async Task<FollowUpEditViewModel?> GetFollowUpForEditAsync(
        int id,
        string currentUserId,
        IList<string> roles)
    {
        var followUp = await _context.FollowUps
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (followUp == null)
        {
            return null;
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && followUp.AssignedToUserId != currentUserId)
        {
            return null;
        }

        var vm = new FollowUpEditViewModel
        {
            Id = followUp.Id,
            Subject = followUp.Subject,
            ScheduledDate = followUp.ScheduledDate,
            Type = followUp.Type,
            Priority = followUp.Priority,
            Status = followUp.Status,
            Notes = followUp.Notes,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            OpportunityId = followUp.OpportunityId,
            AssignedToUserId = followUp.AssignedToUserId,
            CanAssignAny = isPrivileged
        };

        await PopulateEditDropdownsAsync(vm, currentUserId, isPrivileged);
        return vm;
    }

    public async Task<(bool Success, string? ErrorMessage, int FollowUpId)> CreateFollowUpAsync(
        FollowUpCreateViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // 1. Mandatory Business Rule: Follow-up date is required
        if (model.ScheduledDate == default)
        {
            return (false, "Follow-up date and time is required.", 0);
        }

        // 2. Mandatory Business Rule: For a NEW/PLANNED follow-up, FollowUpDate cannot be earlier than today
        if (model.ScheduledDate.Date < DateTime.Today)
        {
            return (false, "Follow-up date cannot be earlier than today for a new or planned follow-up.", 0);
        }

        // 3. Mandatory Business Rule: Follow-up must be associated with at least one valid CRM record
        if (!model.CustomerId.HasValue && !model.LeadId.HasValue && !model.OpportunityId.HasValue)
        {
            return (false, "Follow-up must be associated with at least one valid CRM record (Customer, Lead, or Opportunity).", 0);
        }

        if (model.CustomerId.HasValue)
        {
            var customerExists = await _context.Customers.AnyAsync(c => c.Id == model.CustomerId.Value);
            if (!customerExists)
            {
                return (false, $"Customer #{model.CustomerId.Value} does not exist.", 0);
            }
        }

        if (model.LeadId.HasValue)
        {
            var leadExists = await _context.Leads.AnyAsync(l => l.Id == model.LeadId.Value);
            if (!leadExists)
            {
                return (false, $"Lead #{model.LeadId.Value} does not exist.", 0);
            }
        }

        if (model.OpportunityId.HasValue)
        {
            var oppExists = await _context.Opportunities.AnyAsync(o => o.Id == model.OpportunityId.Value);
            if (!oppExists)
            {
                return (false, $"Opportunity #{model.OpportunityId.Value} does not exist.", 0);
            }
        }

        // 4. Mandatory Business Rule: Assigned user must be valid
        string assignedUser = currentUserId;
        if (isPrivileged && !string.IsNullOrEmpty(model.AssignedToUserId))
        {
            var userExists = await _userManager.FindByIdAsync(model.AssignedToUserId);
            if (userExists == null)
            {
                return (false, "Assigned user is invalid or does not exist.", 0);
            }
            assignedUser = model.AssignedToUserId;
        }

        var followUp = new FollowUp
        {
            Subject = model.Subject.Trim(),
            ScheduledDate = model.ScheduledDate,
            Type = model.Type,
            Priority = model.Priority,
            Status = FollowUpStatus.Planned,
            Notes = model.Notes?.Trim(),
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            OpportunityId = model.OpportunityId,
            AssignedToUserId = assignedUser,
            CreatedAt = DateTime.UtcNow
        };

        _context.FollowUps.Add(followUp);
        await _context.SaveChangesAsync();

        // Add Initial Activity for Follow-Up Scheduled
        _context.Activities.Add(new Activity
        {
            Type = MapFollowUpTypeToActivityType(followUp.Type),
            Status = ActivityStatus.Planned,
            Subject = $"Follow-Up Scheduled: {followUp.Subject}",
            Description = $"Follow-up scheduled for {followUp.ScheduledDate:MMM dd, yyyy HH:mm} ({followUp.Type}). Notes: {followUp.Notes}",
            ActivityDate = followUp.ScheduledDate,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            OpportunityId = followUp.OpportunityId,
            UserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        });

        // Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "CreateFollowUp",
            EntityName = "FollowUp",
            EntityId = followUp.Id.ToString(),
            Details = $"Created follow-up '{followUp.Subject}' ({followUp.Type}) scheduled for {followUp.ScheduledDate:yyyy-MM-dd HH:mm}. Assigned to user {assignedUser}.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return (true, null, followUp.Id);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateFollowUpAsync(
        FollowUpEditViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var followUp = await _context.FollowUps.FirstOrDefaultAsync(f => f.Id == model.Id);
        if (followUp == null)
        {
            return (false, "Follow-up record not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && followUp.AssignedToUserId != currentUserId)
        {
            return (false, "Access denied. You can only edit follow-ups assigned to you.");
        }

        // Validate date
        if (model.ScheduledDate == default)
        {
            return (false, "Scheduled date is required.");
        }

        // Rule: Cancelled follow-ups cannot be completed without reactivation
        if (followUp.Status == FollowUpStatus.Cancelled && model.Status == FollowUpStatus.Completed)
        {
            return (false, "Cancelled follow-ups cannot be completed without an explicit reactivation workflow.");
        }

        // Validate related record
        if (!model.CustomerId.HasValue && !model.LeadId.HasValue && !model.OpportunityId.HasValue)
        {
            return (false, "Follow-up must be associated with at least one valid CRM record.");
        }

        var isRescheduled = followUp.ScheduledDate != model.ScheduledDate;
        var oldDate = followUp.ScheduledDate;

        followUp.Subject = model.Subject.Trim();
        followUp.ScheduledDate = model.ScheduledDate;
        followUp.Type = model.Type;
        followUp.Priority = model.Priority;
        followUp.Status = model.Status;
        followUp.Notes = model.Notes?.Trim();
        followUp.CustomerId = model.CustomerId;
        followUp.LeadId = model.LeadId;
        followUp.OpportunityId = model.OpportunityId;

        if (isPrivileged && !string.IsNullOrEmpty(model.AssignedToUserId))
        {
            followUp.AssignedToUserId = model.AssignedToUserId;
        }

        if (model.Status == FollowUpStatus.Completed && !followUp.CompletedAt.HasValue)
        {
            followUp.CompletedAt = DateTime.UtcNow;
        }

        // Audit Logs
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "UpdateFollowUp",
            EntityName = "FollowUp",
            EntityId = followUp.Id.ToString(),
            Details = $"Updated follow-up '{followUp.Subject}'. Status: {followUp.Status}, Type: {followUp.Type}.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        if (isRescheduled)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "RescheduleFollowUp",
                EntityName = "FollowUp",
                EntityId = followUp.Id.ToString(),
                Details = $"Rescheduled follow-up '{followUp.Subject}' from {oldDate:yyyy-MM-dd HH:mm} to {model.ScheduledDate:yyyy-MM-dd HH:mm}. Reason: {model.ChangeReason ?? "Not specified"}",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> CompleteFollowUpAsync(
        int id,
        string? notes,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var followUp = await _context.FollowUps.FirstOrDefaultAsync(f => f.Id == id);
        if (followUp == null)
        {
            return (false, "Follow-up record not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && followUp.AssignedToUserId != currentUserId)
        {
            return (false, "Access denied. You can only complete follow-ups assigned to you.");
        }

        // Mandatory Business Rule 6: Cancelled follow-ups cannot be completed without reactivation
        if (followUp.Status == FollowUpStatus.Cancelled)
        {
            return (false, "Cancelled follow-ups cannot be completed without an explicit reactivation workflow.");
        }

        followUp.Status = FollowUpStatus.Completed;
        followUp.CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            followUp.Notes = string.IsNullOrWhiteSpace(followUp.Notes) 
                ? $"Completed: {notes.Trim()}" 
                : $"{followUp.Notes}\n\n[Completion Notes]: {notes.Trim()}";
        }

        // Add Activity for Completed Follow-Up
        _context.Activities.Add(new Activity
        {
            Type = MapFollowUpTypeToActivityType(followUp.Type),
            Status = ActivityStatus.Completed,
            Subject = $"Follow-Up Completed: {followUp.Subject}",
            Description = $"Follow-up completed by {currentUserName}. Outcome: {notes ?? "No additional remarks"}",
            ActivityDate = DateTime.UtcNow,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            OpportunityId = followUp.OpportunityId,
            UserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        });

        // Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "CompleteFollowUp",
            EntityName = "FollowUp",
            EntityId = followUp.Id.ToString(),
            Details = $"Completed follow-up '{followUp.Subject}'. Outcome/Notes: {notes ?? "Completed without extra remarks"}.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> MarkMissedFollowUpAsync(
        int id,
        string? reason,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var followUp = await _context.FollowUps.FirstOrDefaultAsync(f => f.Id == id);
        if (followUp == null)
        {
            return (false, "Follow-up record not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && followUp.AssignedToUserId != currentUserId)
        {
            return (false, "Access denied. You can only mark your assigned follow-ups as missed.");
        }

        followUp.Status = FollowUpStatus.Missed;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            followUp.Notes = string.IsNullOrWhiteSpace(followUp.Notes)
                ? $"Missed: {reason.Trim()}"
                : $"{followUp.Notes}\n\n[Missed Reason]: {reason.Trim()}";
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "MissFollowUp",
            EntityName = "FollowUp",
            EntityId = followUp.Id.ToString(),
            Details = $"Marked follow-up '{followUp.Subject}' as Missed. Reason: {reason ?? "No reason specified"}.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> CancelFollowUpAsync(
        int id,
        string? reason,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var followUp = await _context.FollowUps.FirstOrDefaultAsync(f => f.Id == id);
        if (followUp == null)
        {
            return (false, "Follow-up record not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && followUp.AssignedToUserId != currentUserId)
        {
            return (false, "Access denied. You can only cancel your assigned follow-ups.");
        }

        followUp.Status = FollowUpStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            followUp.Notes = string.IsNullOrWhiteSpace(followUp.Notes)
                ? $"Cancelled: {reason.Trim()}"
                : $"{followUp.Notes}\n\n[Cancellation Reason]: {reason.Trim()}";
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "CancelFollowUp",
            EntityName = "FollowUp",
            EntityId = followUp.Id.ToString(),
            Details = $"Cancelled follow-up '{followUp.Subject}'. Reason: {reason ?? "No reason specified"}.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> RescheduleFollowUpAsync(
        int id,
        DateTime newDate,
        string? reason,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var followUp = await _context.FollowUps.FirstOrDefaultAsync(f => f.Id == id);
        if (followUp == null)
        {
            return (false, "Follow-up record not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && followUp.AssignedToUserId != currentUserId)
        {
            return (false, "Access denied. You can only reschedule follow-ups assigned to you.");
        }

        if (newDate.Date < DateTime.Today)
        {
            return (false, "Rescheduled date cannot be in the past.");
        }

        var oldDate = followUp.ScheduledDate;
        followUp.ScheduledDate = newDate;
        followUp.Status = FollowUpStatus.Planned; // Rescheduling resets status back to Planned
        if (!string.IsNullOrWhiteSpace(reason))
        {
            followUp.Notes = string.IsNullOrWhiteSpace(followUp.Notes)
                ? $"Rescheduled from {oldDate:yyyy-MM-dd HH:mm}: {reason.Trim()}"
                : $"{followUp.Notes}\n\n[Rescheduled from {oldDate:yyyy-MM-dd HH:mm}]: {reason.Trim()}";
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "RescheduleFollowUp",
            EntityName = "FollowUp",
            EntityId = followUp.Id.ToString(),
            Details = $"Rescheduled follow-up '{followUp.Subject}' from {oldDate:yyyy-MM-dd HH:mm} to {newDate:yyyy-MM-dd HH:mm}. Reason: {reason ?? "Not specified"}",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    private async Task PopulateCreateDropdownsAsync(FollowUpCreateViewModel vm, string currentUserId, bool isPrivileged)
    {
        var custQuery = _context.Customers.Where(c => c.Status == CustomerStatus.Active);
        if (!isPrivileged) custQuery = custQuery.Where(c => c.AssignedToUserId == currentUserId);
        vm.AvailableCustomers = await custQuery.OrderBy(c => c.CustomerName)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = $"{c.CustomerName} ({c.CustomerCode})" })
            .ToListAsync();

        var leadQuery = _context.Leads.Where(l => l.Status != LeadStatus.Lost);
        if (!isPrivileged) leadQuery = leadQuery.Where(l => l.AssignedToUserId == currentUserId);
        vm.AvailableLeads = await leadQuery.OrderBy(l => l.LeadName)
            .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = $"{l.LeadName} ({l.LeadCode})" })
            .ToListAsync();

        var oppQuery = _context.Opportunities.Where(o => o.Status == OpportunityStatus.Active);
        if (!isPrivileged) oppQuery = oppQuery.Where(o => o.AssignedToUserId == currentUserId);
        vm.AvailableOpportunities = await oppQuery.OrderBy(o => o.OpportunityName)
            .Select(o => new SelectListItem { Value = o.Id.ToString(), Text = $"{o.OpportunityName} ({o.Stage})" })
            .ToListAsync();

        if (isPrivileged)
        {
            var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
            vm.AvailableUsers = users.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.FullName} ({u.Email})",
                Selected = u.Id == currentUserId
            }).ToList();
        }
    }

    private async Task PopulateEditDropdownsAsync(FollowUpEditViewModel vm, string currentUserId, bool isPrivileged)
    {
        var custQuery = _context.Customers.AsQueryable();
        if (!isPrivileged) custQuery = custQuery.Where(c => c.AssignedToUserId == currentUserId);
        vm.AvailableCustomers = await custQuery.OrderBy(c => c.CustomerName)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = $"{c.CustomerName} ({c.CustomerCode})" })
            .ToListAsync();

        var leadQuery = _context.Leads.AsQueryable();
        if (!isPrivileged) leadQuery = leadQuery.Where(l => l.AssignedToUserId == currentUserId);
        vm.AvailableLeads = await leadQuery.OrderBy(l => l.LeadName)
            .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = $"{l.LeadName} ({l.LeadCode})" })
            .ToListAsync();

        var oppQuery = _context.Opportunities.AsQueryable();
        if (!isPrivileged) oppQuery = oppQuery.Where(o => o.AssignedToUserId == currentUserId);
        vm.AvailableOpportunities = await oppQuery.OrderBy(o => o.OpportunityName)
            .Select(o => new SelectListItem { Value = o.Id.ToString(), Text = $"{o.OpportunityName} ({o.Stage})" })
            .ToListAsync();

        if (isPrivileged)
        {
            var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
            vm.AvailableUsers = users.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.FullName} ({u.Email})",
                Selected = u.Id == vm.AssignedToUserId
            }).ToList();
        }
    }

    private static ActivityType MapFollowUpTypeToActivityType(FollowUpType type) => type switch
    {
        FollowUpType.Call => ActivityType.Call,
        FollowUpType.Meeting => ActivityType.Meeting,
        FollowUpType.Email => ActivityType.Email,
        FollowUpType.Task => ActivityType.Task,
        _ => ActivityType.Task
    };
}
