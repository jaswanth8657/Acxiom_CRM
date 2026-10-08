using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Leads;

namespace AcxiomCRM.Services;

public class LeadService : ILeadService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LeadService> _logger;

    public LeadService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<LeadService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<bool> CanUserAccessLeadAsync(int leadId, string currentUserId, IList<string> roles)
    {
        if (roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager))
        {
            return true;
        }

        var lead = await _context.Leads
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leadId);

        if (lead == null)
        {
            return false;
        }

        return lead.AssignedToUserId == currentUserId;
    }

    public async Task<LeadListViewModel> GetPagedLeadsAsync(
        LeadFilterViewModel filter,
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var query = _context.Leads
            .Include(l => l.AssignedToUser)
            .AsNoTracking();

        // 1. Role-based Scoping: SalesExecutive sees only assigned leads
        if (!isPrivileged)
        {
            query = query.Where(l => l.AssignedToUserId == currentUserId);
        }
        else if (!string.IsNullOrEmpty(filter.AssignedToUserId))
        {
            query = query.Where(l => l.AssignedToUserId == filter.AssignedToUserId);
        }

        // 2. Status filter
        if (filter.Status.HasValue)
        {
            query = query.Where(l => l.Status == filter.Status.Value);
        }

        // 3. Source filter
        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            query = query.Where(l => l.Source == filter.Source);
        }

        // 4. Search term filter
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(l =>
                l.LeadName.ToLower().Contains(term) ||
                l.Email.ToLower().Contains(term) ||
                (l.Phone != null && l.Phone.ToLower().Contains(term)) ||
                l.LeadCode.ToLower().Contains(term) ||
                (l.CompanyName != null && l.CompanyName.ToLower().Contains(term)));
        }

        // 5. Total count for pagination
        var totalItems = await query.CountAsync();

        // 6. Pagination calculation
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var items = await query
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LeadListItemViewModel
            {
                Id = l.Id,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Email = l.Email,
                Phone = l.Phone,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                ExpectedValue = l.ExpectedValue,
                AssignedToUserId = l.AssignedToUserId,
                AssignedToUserName = l.AssignedToUser != null ? l.AssignedToUser.FullName : "Unassigned",
                CreatedDate = l.CreatedDate,
                CanEdit = isPrivileged || l.AssignedToUserId == currentUserId,
                CanConvert = l.Status == LeadStatus.Qualified && (isPrivileged || l.AssignedToUserId == currentUserId)
            })
            .ToListAsync();

        // 7. Calculate summary counts within the user's visible scope
        var scopeQuery = _context.Leads.AsNoTracking();
        if (!isPrivileged)
        {
            scopeQuery = scopeQuery.Where(l => l.AssignedToUserId == currentUserId);
        }

        var totalCount = await scopeQuery.CountAsync();
        var newCount = await scopeQuery.CountAsync(l => l.Status == LeadStatus.New);
        var contactedCount = await scopeQuery.CountAsync(l => l.Status == LeadStatus.Contacted);
        var qualifiedCount = await scopeQuery.CountAsync(l => l.Status == LeadStatus.Qualified);
        var convertedCount = await scopeQuery.CountAsync(l => l.Status == LeadStatus.Converted);
        var lostCount = await scopeQuery.CountAsync(l => l.Status == LeadStatus.Lost);

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

        var availableSources = LeadSources.All
            .Select(s => new SelectListItem
            {
                Value = s,
                Text = s,
                Selected = s == filter.Source
            })
            .ToList();

        var availableStatuses = Enum.GetValues<LeadStatus>()
            .Select(s => new SelectListItem
            {
                Value = ((int)s).ToString(),
                Text = s.ToString(),
                Selected = filter.Status.HasValue && filter.Status.Value == s
            })
            .ToList();

        return new LeadListViewModel
        {
            Leads = items,
            Filter = filter,
            TotalItems = totalItems,
            TotalCount = totalCount,
            NewCount = newCount,
            ContactedCount = contactedCount,
            QualifiedCount = qualifiedCount,
            ConvertedCount = convertedCount,
            LostCount = lostCount,
            AvailableUsers = availableUsers,
            AvailableSources = availableSources,
            AvailableStatuses = availableStatuses,
            CanManageAll = isPrivileged
        };
    }

    public async Task<LeadDetailsViewModel?> GetLeadDetailsAsync(
        int id,
        string currentUserId,
        IList<string> roles)
    {
        var lead = await _context.Leads
            .Include(l => l.AssignedToUser)
            .Include(l => l.ConvertedCustomer)
            .Include(l => l.ConvertedOpportunity)
            .Include(l => l.Activities.OrderByDescending(a => a.ActivityDate))
                .ThenInclude(a => a.User)
            .Include(l => l.FollowUps.OrderByDescending(f => f.ScheduledDate))
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lead == null)
        {
            return null;
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        var canAccess = isPrivileged || lead.AssignedToUserId == currentUserId;

        if (!canAccess)
        {
            return null;
        }

        return new LeadDetailsViewModel
        {
            Id = lead.Id,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Title = lead.Title,
            Source = lead.Source,
            Status = lead.Status,
            ExpectedValue = lead.ExpectedValue,
            CreatedDate = lead.CreatedDate,
            CreatedBy = lead.CreatedBy,
            UpdatedAt = lead.UpdatedAt,
            AssignedToUserId = lead.AssignedToUserId,
            AssignedToUserName = lead.AssignedToUser != null ? lead.AssignedToUser.FullName : "Unassigned",
            ConvertedCustomerId = lead.ConvertedCustomerId,
            ConvertedCustomerName = lead.ConvertedCustomer?.CustomerName,
            ConvertedOpportunityId = lead.ConvertedOpportunityId,
            ConvertedOpportunityTitle = lead.ConvertedOpportunity?.Title,
            ConvertedDate = lead.ConvertedDate,
            CanEdit = isPrivileged || lead.AssignedToUserId == currentUserId,
            CanConvert = lead.Status == LeadStatus.Qualified && (isPrivileged || lead.AssignedToUserId == currentUserId),
            CanDelete = isPrivileged || lead.AssignedToUserId == currentUserId,
            Activities = lead.Activities.Select(a => new LeadActivityItemViewModel
            {
                Id = a.Id,
                ActivityType = a.Type.ToString(),
                Subject = a.Subject,
                Description = a.Description,
                ActivityDate = a.ActivityDate,
                PerformedByName = a.User?.FullName ?? "System"
            }).ToList(),
            FollowUps = lead.FollowUps.Select(f => new LeadFollowUpItemViewModel
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

    public async Task<Lead?> GetLeadByIdAsync(int id)
    {
        return await _context.Leads
            .Include(l => l.AssignedToUser)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<(bool Success, string? Error, int? LeadId)> CreateLeadAsync(
        LeadCreateViewModel model,
        string currentUserId,
        string currentUserName,
        string? ipAddress)
    {
        // 1. Ensure Lead Code
        if (string.IsNullOrWhiteSpace(model.LeadCode))
        {
            model.LeadCode = await GenerateLeadCodeAsync();
        }

        // 2. Validate uniqueness of Lead Code
        if (!await IsLeadCodeUniqueAsync(model.LeadCode))
        {
            return (false, "Lead Code already exists.", null);
        }

        // 3. Validate uniqueness of Email
        if (!await IsEmailUniqueAsync(model.Email))
        {
            return (false, "Email already exists.", null);
        }

        // 4. Validate ExpectedValue
        if (model.ExpectedValue.HasValue && model.ExpectedValue.Value < 0)
        {
            return (false, "Expected Value cannot be negative.", null);
        }

        // 5. Construct Lead entity
        var lead = new Lead
        {
            LeadCode = model.LeadCode.Trim().ToUpper(),
            LeadName = model.LeadName.Trim(),
            Email = model.Email.Trim().ToLower(),
            Phone = !string.IsNullOrWhiteSpace(model.Phone) ? model.Phone.Trim() : null,
            CompanyName = !string.IsNullOrWhiteSpace(model.CompanyName) ? model.CompanyName.Trim() : null,
            Title = !string.IsNullOrWhiteSpace(model.Title) ? model.Title.Trim() : null,
            Source = !string.IsNullOrWhiteSpace(model.Source) ? model.Source.Trim() : "Website",
            Status = model.Status,
            ExpectedValue = model.ExpectedValue,
            AssignedToUserId = !string.IsNullOrWhiteSpace(model.AssignedToUserId) ? model.AssignedToUserId : currentUserId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = currentUserName
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        // 6. Record Initial Activity
        _context.Activities.Add(new Activity
        {
            LeadId = lead.Id,
            Type = ActivityType.Note,
            Subject = "Lead Created",
            Description = $"Lead created by {currentUserName} with source '{lead.Source}' and initial status '{lead.Status}'.",
            ActivityDate = DateTime.UtcNow,
            UserId = currentUserId
        });

        // 7. Record Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "CreateLead",
            EntityName = "Lead",
            EntityId = lead.Id.ToString(),
            Details = $"Created lead '{lead.LeadName}' (Code: {lead.LeadCode}, Email: {lead.Email}, Status: {lead.Status}, Value: {lead.ExpectedValue:C}).",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Lead {LeadId} created successfully by user {UserId}.", lead.Id, currentUserId);

        return (true, null, lead.Id);
    }

    public async Task<(bool Success, string? Error)> UpdateLeadAsync(
        LeadEditViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var lead = await _context.Leads
            .Include(l => l.AssignedToUser)
            .FirstOrDefaultAsync(l => l.Id == model.Id);

        if (lead == null)
        {
            return (false, "Lead not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Security check
        if (!isPrivileged && lead.AssignedToUserId != currentUserId)
        {
            _logger.LogWarning("Forbidden: User {UserId} attempted to update unauthorized Lead {LeadId}", currentUserId, model.Id);
            return (false, "You are not authorized to update this lead.");
        }

        // Validate uniqueness of Email
        if (!await IsEmailUniqueAsync(model.Email, model.Id))
        {
            return (false, "Email already exists.");
        }

        // Validate ExpectedValue
        if (model.ExpectedValue.HasValue && model.ExpectedValue.Value < 0)
        {
            return (false, "Expected Value cannot be negative.");
        }

        // Prevent modification of converted leads
        if (lead.Status == LeadStatus.Converted && model.Status != LeadStatus.Converted)
        {
            return (false, "Converted leads cannot be moved to another status.");
        }

        // Validate Status Transition
        if (lead.Status != model.Status)
        {
            if (!IsValidStatusTransition(lead.Status, model.Status))
            {
                return (false, $"Invalid status transition from {lead.Status} to {model.Status}.");
            }

            // Log status transition
            var oldStatus = lead.Status;
            lead.Status = model.Status;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "LeadStatusChange",
                EntityName = "Lead",
                EntityId = lead.Id.ToString(),
                Details = $"Status changed from {oldStatus} to {model.Status}. Note: {model.StatusChangeNote ?? "None"}",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            });

            _context.Activities.Add(new Activity
            {
                LeadId = lead.Id,
                Type = ActivityType.Note,
                Subject = $"Status updated to {model.Status}",
                Description = $"Status changed from {oldStatus} to {model.Status}. " + (!string.IsNullOrWhiteSpace(model.StatusChangeNote) ? $"Note: {model.StatusChangeNote}" : string.Empty),
                ActivityDate = DateTime.UtcNow,
                UserId = currentUserId
            });
        }

        // Handle Assignment Changes
        if (lead.AssignedToUserId != model.AssignedToUserId)
        {
            if (!isPrivileged)
            {
                return (false, "Only Admins and Managers can reassign leads.");
            }

            var oldAssignee = lead.AssignedToUser?.FullName ?? "Unassigned";
            var newAssigneeUser = await _userManager.FindByIdAsync(model.AssignedToUserId ?? "");
            var newAssignee = newAssigneeUser?.FullName ?? "Unassigned";

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "LeadAssignmentChange",
                EntityName = "Lead",
                EntityId = lead.Id.ToString(),
                Details = $"Lead '{lead.LeadName}' reassigned from '{oldAssignee}' to '{newAssignee}'.",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            });

            lead.AssignedToUserId = model.AssignedToUserId;
        }

        // Update fields
        lead.LeadName = model.LeadName.Trim();
        lead.Email = model.Email.Trim().ToLower();
        lead.Phone = !string.IsNullOrWhiteSpace(model.Phone) ? model.Phone.Trim() : null;
        lead.CompanyName = !string.IsNullOrWhiteSpace(model.CompanyName) ? model.CompanyName.Trim() : null;
        lead.Title = !string.IsNullOrWhiteSpace(model.Title) ? model.Title.Trim() : null;
        lead.Source = !string.IsNullOrWhiteSpace(model.Source) ? model.Source.Trim() : "Website";
        lead.ExpectedValue = model.ExpectedValue;
        lead.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "UpdateLead",
            EntityName = "Lead",
            EntityId = lead.Id.ToString(),
            Details = $"Updated lead details for '{lead.LeadName}' (Code: {lead.LeadCode}).",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Lead {LeadId} updated by user {UserId}.", lead.Id, currentUserId);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteLeadAsync(
        int id,
        string currentUserId,
        IList<string> roles,
        string? ipAddress)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id);
        if (lead == null)
        {
            return (false, "Lead not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Security check
        if (!isPrivileged && lead.AssignedToUserId != currentUserId)
        {
            _logger.LogWarning("Forbidden: User {UserId} attempted to delete unauthorized Lead {LeadId}", currentUserId, id);
            return (false, "You are not authorized to delete this lead.");
        }

        var leadName = lead.LeadName;
        var leadCode = lead.LeadCode;

        _context.Leads.Remove(lead);

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "DeleteLead",
            EntityName = "Lead",
            EntityId = id.ToString(),
            Details = $"Deleted lead '{leadName}' (Code: {leadCode}).",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Lead {LeadId} deleted by user {UserId}.", id, currentUserId);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ChangeStatusAsync(
        int leadId,
        LeadStatus newStatus,
        string? note,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == leadId);
        if (lead == null)
        {
            return (false, "Lead not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        if (!isPrivileged && lead.AssignedToUserId != currentUserId)
        {
            _logger.LogWarning("Forbidden: User {UserId} attempted status change on unauthorized Lead {LeadId}", currentUserId, leadId);
            return (false, "You are not authorized to modify this lead.");
        }

        if (lead.Status == newStatus)
        {
            return (true, null);
        }

        if (lead.Status == LeadStatus.Converted)
        {
            return (false, "Converted leads cannot be moved to another status.");
        }

        if (!IsValidStatusTransition(lead.Status, newStatus))
        {
            return (false, $"Invalid status transition from {lead.Status} to {newStatus}.");
        }

        var oldStatus = lead.Status;
        lead.Status = newStatus;
        lead.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "LeadStatusChange",
            EntityName = "Lead",
            EntityId = lead.Id.ToString(),
            Details = $"Status changed from {oldStatus} to {newStatus}. Note: {note ?? "None"}",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });

        _context.Activities.Add(new Activity
        {
            LeadId = lead.Id,
            Type = ActivityType.Note,
            Subject = $"Status updated to {newStatus}",
            Description = $"Status changed from {oldStatus} to {newStatus}. " + (!string.IsNullOrWhiteSpace(note) ? $"Note: {note}" : string.Empty),
            ActivityDate = DateTime.UtcNow,
            UserId = currentUserId
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error, int? CustomerId, int? OpportunityId)> ConvertLeadAsync(
        LeadConvertViewModel model,
        string currentUserId,
        string currentUserName,
        IList<string> roles,
        string? ipAddress)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == model.LeadId);
        if (lead == null)
        {
            return (false, "Lead not found.", null, null);
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Cross-user authorization check
        if (!isPrivileged && lead.AssignedToUserId != currentUserId)
        {
            _logger.LogWarning("Forbidden: User {UserId} attempted to convert unauthorized Lead {LeadId}", currentUserId, model.LeadId);
            return (false, "You are not authorized to convert this lead.", null, null);
        }

        // Eligibility validation
        if (lead.Status == LeadStatus.Converted)
        {
            return (false, "Lead is already converted.", null, null);
        }

        if (lead.Status != LeadStatus.Qualified)
        {
            return (false, "Only qualified leads can be converted. Please qualify the lead first.", null, null);
        }

        // Atomic DB transaction
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Check duplicate customer by email or phone
            Customer? customer = null;
            var normalizedEmail = lead.Email.Trim().ToLower();
            var phone = !string.IsNullOrWhiteSpace(lead.Phone) ? lead.Phone.Trim() : null;

            customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email.ToLower() == normalizedEmail || (phone != null && c.Phone == phone));

            if (customer == null)
            {
                // Create new Customer
                var customerCode = await GenerateCustomerCodeInternalAsync();
                customer = new Customer
                {
                    CustomerCode = customerCode,
                    CustomerName = lead.LeadName,
                    Email = lead.Email,
                    Phone = !string.IsNullOrWhiteSpace(lead.Phone) ? lead.Phone : "N/A",
                    CompanyName = lead.CompanyName,
                    Status = CustomerStatus.Active,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = currentUserName,
                    AssignedToUserId = lead.AssignedToUserId
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = "CreateCustomer",
                    EntityName = "Customer",
                    EntityId = customer.Id.ToString(),
                    Details = $"Created customer '{customer.CustomerName}' from converted lead '{lead.LeadName}' (Code: {customer.CustomerCode}).",
                    IpAddress = ipAddress,
                    Timestamp = DateTime.UtcNow
                });
            }

            // 2. Create Opportunity
            var oppTitle = string.IsNullOrWhiteSpace(model.OpportunityTitle)
                ? $"{lead.CompanyName ?? lead.LeadName} - Deal"
                : model.OpportunityTitle.Trim();

            var oppAmount = model.OpportunityAmount ?? lead.ExpectedValue ?? 0m;
            var expectedClose = model.ExpectedCloseDate ?? DateTime.UtcNow.AddDays(30);

            var opportunity = new Opportunity
            {
                OpportunityName = oppTitle,
                Amount = oppAmount,
                Stage = model.OpportunityStage,
                Probability = 25,
                ExpectedCloseDate = expectedClose,
                CustomerId = customer.Id,
                AssignedToUserId = lead.AssignedToUserId,
                LeadId = lead.Id,
                CreatedDate = DateTime.UtcNow
            };

            _context.Opportunities.Add(opportunity);
            await _context.SaveChangesAsync();

            // 3. Update Lead to Converted
            lead.Status = LeadStatus.Converted;
            lead.ConvertedCustomerId = customer.Id;
            lead.ConvertedOpportunityId = opportunity.Id;
            lead.ConvertedDate = DateTime.UtcNow;
            lead.UpdatedAt = DateTime.UtcNow;

            // 4. Create Activity
            _context.Activities.Add(new Activity
            {
                LeadId = lead.Id,
                CustomerId = customer.Id,
                OpportunityId = opportunity.Id,
                Type = ActivityType.Note,
                Subject = "Lead Converted",
                Description = $"Lead successfully converted by {currentUserName}. Linked to Customer '{customer.CustomerName}' (ID: {customer.Id}) and Opportunity '{opportunity.Title}' (ID: {opportunity.Id}, Amount: {opportunity.Amount:C}).",
                ActivityDate = DateTime.UtcNow,
                UserId = currentUserId
            });

            // 5. Create AuditLog for LeadConversion
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "LeadConversion",
                EntityName = "Lead",
                EntityId = lead.Id.ToString(),
                Details = $"Lead '{lead.LeadName}' (Code: {lead.LeadCode}) converted to Customer '{customer.CustomerName}' (ID: {customer.Id}) and Opportunity '{opportunity.Title}' (ID: {opportunity.Id}). Amount: {opportunity.Amount:C}.",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // 6. Commit transaction
            await transaction.CommitAsync();

            _logger.LogInformation("Lead {LeadId} successfully converted to Customer {CustomerId} and Opportunity {OpportunityId}.", lead.Id, customer.Id, opportunity.Id);
            return (true, null, customer.Id, opportunity.Id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error occurred during lead conversion for Lead {LeadId}", model.LeadId);
            return (false, $"An error occurred during conversion: {ex.Message}", null, null);
        }
    }

    public async Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return true;
        var normalizedEmail = email.Trim().ToLower();

        var query = _context.Leads.Where(l => l.Email.ToLower() == normalizedEmail);
        if (excludeId.HasValue)
        {
            query = query.Where(l => l.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<bool> IsLeadCodeUniqueAsync(string leadCode, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(leadCode)) return true;
        var normalizedCode = leadCode.Trim().ToUpper();

        var query = _context.Leads.Where(l => l.LeadCode.ToUpper() == normalizedCode);
        if (excludeId.HasValue)
        {
            query = query.Where(l => l.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<string> GenerateLeadCodeAsync()
    {
        var count = await _context.Leads.CountAsync();
        var code = $"LED-{(count + 1):D4}";

        int suffix = 1;
        while (await _context.Leads.AnyAsync(l => l.LeadCode == code))
        {
            code = $"LED-{(count + 1 + suffix):D4}";
            suffix++;
        }

        return code;
    }

    private async Task<string> GenerateCustomerCodeInternalAsync()
    {
        var count = await _context.Customers.CountAsync();
        var code = $"CUST-{(count + 1):D4}";

        int suffix = 1;
        while (await _context.Customers.AnyAsync(c => c.CustomerCode == code))
        {
            code = $"CUST-{(count + 1 + suffix):D4}";
            suffix++;
        }

        return code;
    }

    public bool IsValidStatusTransition(LeadStatus currentStatus, LeadStatus targetStatus)
    {
        if (currentStatus == targetStatus) return true;

        return (currentStatus, targetStatus) switch
        {
            (LeadStatus.New, LeadStatus.Contacted) => true,
            (LeadStatus.New, LeadStatus.Qualified) => true,
            (LeadStatus.New, LeadStatus.Unqualified) => true,
            (LeadStatus.New, LeadStatus.Lost) => true,

            (LeadStatus.Contacted, LeadStatus.Qualified) => true,
            (LeadStatus.Contacted, LeadStatus.Unqualified) => true,
            (LeadStatus.Contacted, LeadStatus.Lost) => true,

            (LeadStatus.Qualified, LeadStatus.Converted) => true,
            (LeadStatus.Qualified, LeadStatus.Lost) => true,
            (LeadStatus.Qualified, LeadStatus.Unqualified) => true,

            (LeadStatus.Unqualified, LeadStatus.Contacted) => true,
            (LeadStatus.Unqualified, LeadStatus.Qualified) => true,
            (LeadStatus.Unqualified, LeadStatus.Lost) => true,

            (LeadStatus.Converted, _) => false,
            (LeadStatus.Lost, _) => false,

            _ => false
        };
    }

    public List<LeadStatus> GetAllowedNextStatuses(LeadStatus currentStatus)
    {
        var list = new List<LeadStatus> { currentStatus };
        foreach (LeadStatus s in Enum.GetValues<LeadStatus>())
        {
            if (s != currentStatus && IsValidStatusTransition(currentStatus, s))
            {
                list.Add(s);
            }
        }
        return list;
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
