using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Activities;

namespace AcxiomCRM.Services;

public class ActivityService : IActivityService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ActivityService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<bool> CanUserAccessActivityAsync(
        int id,
        string currentUserId,
        IList<string> roles)
    {
        if (roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager))
        {
            return true;
        }

        var activity = await _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return false;
        }

        return activity.UserId == currentUserId ||
               (activity.Customer != null && activity.Customer.AssignedToUserId == currentUserId) ||
               (activity.Lead != null && activity.Lead.AssignedToUserId == currentUserId) ||
               (activity.Opportunity != null && activity.Opportunity.AssignedToUserId == currentUserId);
    }

    public async Task<ActivityListViewModel> GetPagedActivitiesAsync(
        ActivityFilterViewModel filter,
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var query = _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .Include(a => a.User)
            .AsNoTracking();

        // 1. Role-based scoping
        if (!isPrivileged)
        {
            query = query.Where(a =>
                a.UserId == currentUserId ||
                (a.Customer != null && a.Customer.AssignedToUserId == currentUserId) ||
                (a.Lead != null && a.Lead.AssignedToUserId == currentUserId) ||
                (a.Opportunity != null && a.Opportunity.AssignedToUserId == currentUserId));
        }
        else if (!string.IsNullOrEmpty(filter.UserId))
        {
            query = query.Where(a => a.UserId == filter.UserId);
        }

        // 2. Type Filter
        if (filter.Type.HasValue)
        {
            query = query.Where(a => a.Type == filter.Type.Value);
        }

        // 3. Status Filter
        if (filter.Status.HasValue)
        {
            query = query.Where(a => a.Status == filter.Status.Value);
        }

        // 4. Date Range
        if (filter.StartDate.HasValue)
        {
            var start = filter.StartDate.Value.Date;
            query = query.Where(a => a.ActivityDate >= start);
        }
        if (filter.EndDate.HasValue)
        {
            var end = filter.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(a => a.ActivityDate <= end);
        }

        // 5. Search Filter
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Subject.ToLower().Contains(term) ||
                (a.Description != null && a.Description.ToLower().Contains(term)) ||
                (a.Customer != null && a.Customer.CustomerName.ToLower().Contains(term)) ||
                (a.Lead != null && a.Lead.LeadName.ToLower().Contains(term)) ||
                (a.Opportunity != null && a.Opportunity.OpportunityName.ToLower().Contains(term)));
        }

        // 6. Pagination
        var totalItems = await query.CountAsync();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var items = await query
            .OrderByDescending(a => a.ActivityDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityListItemViewModel
            {
                Id = a.Id,
                Type = a.Type,
                Status = a.Status,
                Subject = a.Subject,
                Description = a.Description,
                ActivityDate = a.ActivityDate,
                CustomerId = a.CustomerId,
                CustomerName = a.Customer != null ? a.Customer.CustomerName : null,
                LeadId = a.LeadId,
                LeadName = a.Lead != null ? a.Lead.LeadName : null,
                OpportunityId = a.OpportunityId,
                OpportunityName = a.Opportunity != null ? a.Opportunity.OpportunityName : null,
                UserId = a.UserId,
                UserName = a.User != null ? a.User.FullName : "Unknown",
                CreatedAt = a.CreatedAt,
                CanEdit = isPrivileged || a.UserId == currentUserId,
                CanDelete = isPrivileged || a.UserId == currentUserId
            })
            .ToListAsync();

        var availableUsers = new List<SelectListItem>();
        if (isPrivileged)
        {
            var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
            availableUsers = users.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.FullName} ({u.Email})",
                Selected = u.Id == filter.UserId
            }).ToList();
        }

        var availableTypes = Enum.GetValues<ActivityType>()
            .Select(t => new SelectListItem
            {
                Value = t.ToString(),
                Text = t.ToString(),
                Selected = filter.Type.HasValue && filter.Type.Value == t
            }).ToList();

        var availableStatuses = Enum.GetValues<ActivityStatus>()
            .Select(s => new SelectListItem
            {
                Value = s.ToString(),
                Text = s.ToString(),
                Selected = filter.Status.HasValue && filter.Status.Value == s
            }).ToList();

        return new ActivityListViewModel
        {
            Activities = items,
            Filter = filter,
            TotalItems = totalItems,
            AvailableUsers = availableUsers,
            AvailableTypes = availableTypes,
            AvailableStatuses = availableStatuses,
            CanManageAll = isPrivileged
        };
    }

    public async Task<ActivityDetailsViewModel?> GetActivityDetailsAsync(
        int id,
        string currentUserId,
        IList<string> roles)
    {
        var activity = await _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .Include(a => a.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return null;
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged)
        {
            var hasAccess = activity.UserId == currentUserId ||
                            (activity.Customer != null && activity.Customer.AssignedToUserId == currentUserId) ||
                            (activity.Lead != null && activity.Lead.AssignedToUserId == currentUserId) ||
                            (activity.Opportunity != null && activity.Opportunity.AssignedToUserId == currentUserId);
            if (!hasAccess)
            {
                return null;
            }
        }

        return new ActivityDetailsViewModel
        {
            Id = activity.Id,
            Type = activity.Type,
            Status = activity.Status,
            Subject = activity.Subject,
            Description = activity.Description,
            ActivityDate = activity.ActivityDate,
            CustomerId = activity.CustomerId,
            CustomerName = activity.Customer?.CustomerName,
            LeadId = activity.LeadId,
            LeadName = activity.Lead?.LeadName,
            OpportunityId = activity.OpportunityId,
            OpportunityName = activity.Opportunity?.OpportunityName,
            UserId = activity.UserId,
            UserName = activity.User?.FullName ?? "Unknown",
            CreatedAt = activity.CreatedAt,
            CanEdit = isPrivileged || activity.UserId == currentUserId,
            CanDelete = isPrivileged || activity.UserId == currentUserId
        };
    }

    public async Task<ActivityCreateViewModel> GetActivityCreateViewModelAsync(
        string currentUserId,
        IList<string> roles,
        int? customerId = null,
        int? leadId = null,
        int? opportunityId = null)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var vm = new ActivityCreateViewModel
        {
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId,
            ActivityDate = DateTime.Now,
            UserId = currentUserId,
            CanAssignAny = isPrivileged
        };

        await PopulateCreateDropdownsAsync(vm, currentUserId, isPrivileged);
        return vm;
    }

    public async Task<ActivityEditViewModel?> GetActivityForEditAsync(
        int id,
        string currentUserId,
        IList<string> roles)
    {
        var activity = await _context.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return null;
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && activity.UserId != currentUserId)
        {
            return null;
        }

        var vm = new ActivityEditViewModel
        {
            Id = activity.Id,
            Subject = activity.Subject,
            Description = activity.Description,
            Type = activity.Type,
            Status = activity.Status,
            ActivityDate = activity.ActivityDate,
            CustomerId = activity.CustomerId,
            LeadId = activity.LeadId,
            OpportunityId = activity.OpportunityId,
            UserId = activity.UserId,
            CanAssignAny = isPrivileged
        };

        await PopulateEditDropdownsAsync(vm, currentUserId, isPrivileged);
        return vm;
    }

    public async Task<(bool Success, string? ErrorMessage, int ActivityId)> CreateActivityAsync(
        ActivityCreateViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        if (string.IsNullOrWhiteSpace(model.Subject))
        {
            return (false, "Subject is required.", 0);
        }

        if (model.ActivityDate == default)
        {
            return (false, "Activity Date is required.", 0);
        }

        // Must be associated with at least one CRM record
        if (!model.CustomerId.HasValue && !model.LeadId.HasValue && !model.OpportunityId.HasValue)
        {
            return (false, "Activity must be associated with at least one CRM record (Customer, Lead, or Opportunity).", 0);
        }

        // Validate related records
        if (model.CustomerId.HasValue)
        {
            var customer = await _context.Customers.FindAsync(model.CustomerId.Value);
            if (customer == null)
            {
                return (false, $"Customer #{model.CustomerId.Value} does not exist.", 0);
            }
            if (!isPrivileged && customer.AssignedToUserId != currentUserId)
            {
                return (false, "Access denied. You cannot log an activity against an unassigned customer.", 0);
            }
        }

        if (model.LeadId.HasValue)
        {
            var lead = await _context.Leads.FindAsync(model.LeadId.Value);
            if (lead == null)
            {
                return (false, $"Lead #{model.LeadId.Value} does not exist.", 0);
            }
            if (!isPrivileged && lead.AssignedToUserId != currentUserId)
            {
                return (false, "Access denied. You cannot log an activity against an unassigned lead.", 0);
            }
        }

        if (model.OpportunityId.HasValue)
        {
            var opp = await _context.Opportunities.FindAsync(model.OpportunityId.Value);
            if (opp == null)
            {
                return (false, $"Opportunity #{model.OpportunityId.Value} does not exist.", 0);
            }
            if (!isPrivileged && opp.AssignedToUserId != currentUserId)
            {
                return (false, "Access denied. You cannot log an activity against an unassigned opportunity.", 0);
            }
        }

        string userId = currentUserId;
        if (isPrivileged && !string.IsNullOrEmpty(model.UserId))
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return (false, "Assigned user does not exist.", 0);
            }
            userId = model.UserId;
        }

        var activity = new Activity
        {
            Subject = model.Subject.Trim(),
            Description = model.Description?.Trim(),
            Type = model.Type,
            Status = model.Status,
            ActivityDate = model.ActivityDate,
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            OpportunityId = model.OpportunityId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "CreateActivity",
            EntityName = "Activity",
            EntityId = activity.Id.ToString(),
            Details = $"Created activity '{activity.Subject}' ({activity.Type}, {activity.Status}). Performed by user {userId}.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null, activity.Id);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateActivityAsync(
        ActivityEditViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == model.Id);
        if (activity == null)
        {
            return (false, "Activity record not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && activity.UserId != currentUserId)
        {
            return (false, "Access denied. You can only edit activities you created.");
        }

        activity.Subject = model.Subject.Trim();
        activity.Description = model.Description?.Trim();
        activity.Type = model.Type;
        activity.Status = model.Status;
        activity.ActivityDate = model.ActivityDate;
        activity.CustomerId = model.CustomerId;
        activity.LeadId = model.LeadId;
        activity.OpportunityId = model.OpportunityId;

        if (isPrivileged && !string.IsNullOrEmpty(model.UserId))
        {
            activity.UserId = model.UserId;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "UpdateActivity",
            EntityName = "Activity",
            EntityId = activity.Id.ToString(),
            Details = $"Updated activity '{activity.Subject}'. Type: {activity.Type}, Status: {activity.Status}.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> CompleteActivityAsync(
        int id,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null)
        {
            return (false, "Activity record not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && activity.UserId != currentUserId)
        {
            return (false, "Access denied. You can only complete your own activities.");
        }

        activity.Status = ActivityStatus.Completed;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "CompleteActivity",
            EntityName = "Activity",
            EntityId = activity.Id.ToString(),
            Details = $"Completed activity '{activity.Subject}'.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> CancelActivityAsync(
        int id,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null)
        {
            return (false, "Activity record not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && activity.UserId != currentUserId)
        {
            return (false, "Access denied. You can only cancel your own activities.");
        }

        activity.Status = ActivityStatus.Cancelled;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "CancelActivity",
            EntityName = "Activity",
            EntityId = activity.Id.ToString(),
            Details = $"Cancelled activity '{activity.Subject}'.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteActivityAsync(
        int id,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null)
        {
            return (false, "Activity record not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && activity.UserId != currentUserId)
        {
            return (false, "Access denied. You can only delete your own activities.");
        }

        _context.Activities.Remove(activity);

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "DeleteActivity",
            EntityName = "Activity",
            EntityId = id.ToString(),
            Details = $"Deleted activity '{activity.Subject}' (ID: {id}).",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    private async Task PopulateCreateDropdownsAsync(ActivityCreateViewModel vm, string currentUserId, bool isPrivileged)
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

    private async Task PopulateEditDropdownsAsync(ActivityEditViewModel vm, string currentUserId, bool isPrivileged)
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
                Selected = u.Id == vm.UserId
            }).ToList();
        }
    }
}
