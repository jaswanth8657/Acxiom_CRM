using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Customers;

namespace AcxiomCRM.Services;

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<CustomerService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<bool> CanUserAccessCustomerAsync(int customerId, string currentUserId, IList<string> roles)
    {
        if (roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager))
        {
            return true;
        }

        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null)
        {
            return false;
        }

        return customer.AssignedToUserId == currentUserId;
    }

    public async Task<CustomerListViewModel> GetPagedCustomersAsync(
        CustomerFilterViewModel filter,
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var query = _context.Customers
            .Include(c => c.AssignedToUser)
            .AsNoTracking();

        // 1. Role-based scoping: SalesExecutive sees only assigned customers
        if (!isPrivileged)
        {
            query = query.Where(c => c.AssignedToUserId == currentUserId);
        }
        else if (!string.IsNullOrEmpty(filter.AssignedToUserId))
        {
            query = query.Where(c => c.AssignedToUserId == filter.AssignedToUserId);
        }

        // 2. Status filter
        if (filter.Status.HasValue)
        {
            query = query.Where(c => c.Status == filter.Status.Value);
        }

        // 3. Search term filter
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(c =>
                c.CustomerName.ToLower().Contains(term) ||
                c.Email.ToLower().Contains(term) ||
                c.Phone.ToLower().Contains(term) ||
                c.CustomerCode.ToLower().Contains(term) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)) ||
                (c.City != null && c.City.ToLower().Contains(term)));
        }

        // 4. Total count for pagination
        var totalItems = await query.CountAsync();

        // 5. Pagination calculation
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var items = await query
            .OrderByDescending(c => c.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerListItemViewModel
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                City = c.City,
                State = c.State,
                Status = c.Status,
                AssignedToUserId = c.AssignedToUserId,
                AssignedToUserName = c.AssignedToUser != null ? c.AssignedToUser.FullName : "Unassigned",
                CreatedDate = c.CreatedDate
            })
            .ToListAsync();

        // 6. Available users for filter dropdown (if privileged)
        var availableUsers = new List<SelectListItem>();
        if (isPrivileged)
        {
            var users = await _userManager.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FullName} ({u.Email})"
                })
                .ToListAsync();
            availableUsers = users;
        }

        return new CustomerListViewModel
        {
            Customers = items,
            Filter = filter,
            TotalItems = totalItems,
            AvailableUsers = availableUsers,
            CanManageAll = isPrivileged
        };
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id, string currentUserId, IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var customer = await _context.Customers
            .Include(c => c.AssignedToUser)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer == null)
        {
            return null;
        }

        if (!isPrivileged && customer.AssignedToUserId != currentUserId)
        {
            _logger.LogWarning("Access Denied: User {UserId} attempted to access Customer {CustomerId} assigned to {AssignedTo}",
                currentUserId, id, customer.AssignedToUserId);
            return null;
        }

        return customer;
    }

    public async Task<CustomerDetailsViewModel?> GetCustomerDetailsAsync(int id, string currentUserId, IList<string> roles)
    {
        var customer = await GetCustomerByIdAsync(id, currentUserId, roles);
        if (customer == null)
        {
            return null;
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Fetch related activities, opportunities, followups
        var activities = await _context.Activities
            .Include(a => a.User)
            .Where(a => a.CustomerId == id)
            .OrderByDescending(a => a.ActivityDate)
            .Take(10)
            .Select(a => new CustomerActivityItemViewModel
            {
                Id = a.Id,
                Type = a.Type,
                Subject = a.Subject,
                Description = a.Description,
                ActivityDate = a.ActivityDate,
                UserName = a.User.FullName
            })
            .ToListAsync();

        var opportunities = await _context.Opportunities
            .Where(o => o.CustomerId == id)
            .OrderByDescending(o => o.CreatedDate)
            .Take(5)
            .Select(o => new CustomerOpportunityItemViewModel
            {
                Id = o.Id,
                Title = o.OpportunityName,
                Amount = o.Amount,
                Stage = o.Stage,
                ExpectedCloseDate = o.ExpectedCloseDate
            })
            .ToListAsync();

        var followUps = await _context.FollowUps
            .Where(f => f.CustomerId == id)
            .OrderByDescending(f => f.ScheduledDate)
            .Take(5)
            .Select(f => new CustomerFollowUpItemViewModel
            {
                Id = f.Id,
                Subject = f.Subject,
                ScheduledDate = f.ScheduledDate,
                Status = f.Status,
                Priority = f.Priority
            })
            .ToListAsync();

        return new CustomerDetailsViewModel
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            PostalCode = customer.PostalCode,
            Country = customer.Country,
            Industry = customer.Industry,
            Status = customer.Status,
            CreatedDate = customer.CreatedDate,
            UpdatedAt = customer.UpdatedAt,
            CreatedBy = customer.CreatedBy,
            AssignedToUserId = customer.AssignedToUserId,
            AssignedToUserName = customer.AssignedToUser?.FullName ?? "Unassigned",
            CanEdit = isPrivileged || customer.AssignedToUserId == currentUserId,
            CanDelete = isPrivileged || customer.AssignedToUserId == currentUserId,
            Activities = activities,
            Opportunities = opportunities,
            FollowUps = followUps
        };
    }

    public async Task<CustomerEditViewModel?> GetCustomerForEditAsync(int id, string currentUserId, IList<string> roles)
    {
        var customer = await GetCustomerByIdAsync(id, currentUserId, roles);
        if (customer == null)
        {
            return null;
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var availableUsers = new List<SelectListItem>();
        if (isPrivileged)
        {
            availableUsers = await _userManager.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FullName} ({u.Email})",
                    Selected = u.Id == customer.AssignedToUserId
                })
                .ToListAsync();
        }

        return new CustomerEditViewModel
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            PostalCode = customer.PostalCode,
            Country = customer.Country,
            Industry = customer.Industry,
            Status = customer.Status,
            AssignedToUserId = customer.AssignedToUserId,
            AvailableUsers = availableUsers
        };
    }

    public async Task<CustomerDeleteViewModel?> GetCustomerForDeleteAsync(int id, string currentUserId, IList<string> roles)
    {
        var customer = await GetCustomerByIdAsync(id, currentUserId, roles);
        if (customer == null)
        {
            return null;
        }

        return new CustomerDeleteViewModel
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Status = customer.Status,
            AssignedToUserName = customer.AssignedToUser?.FullName ?? "Unassigned"
        };
    }

    public async Task<(bool Success, string? ErrorMessage, Customer? Customer)> CreateCustomerAsync(
        CustomerCreateViewModel model,
        string currentUserId,
        IList<string> roles,
        string? ipAddress)
    {
        // 1. Validate Customer Name
        if (string.IsNullOrWhiteSpace(model.CustomerName))
        {
            return (false, "Customer Name is required.", null);
        }

        // 2. Validate & ensure Customer Code
        if (string.IsNullOrWhiteSpace(model.CustomerCode))
        {
            model.CustomerCode = await GenerateCustomerCodeAsync();
        }
        else if (!await IsCustomerCodeUniqueAsync(model.CustomerCode))
        {
            return (false, "Customer code already exists.", null);
        }

        // 3. Validate unique email
        if (string.IsNullOrWhiteSpace(model.Email))
        {
            return (false, "Enter a valid email address.", null);
        }
        if (!await IsEmailUniqueAsync(model.Email))
        {
            return (false, "Email already exists.", null);
        }

        // 4. Validate unique phone
        if (string.IsNullOrWhiteSpace(model.Phone))
        {
            return (false, "Enter a valid phone number.", null);
        }
        if (!await IsPhoneUniqueAsync(model.Phone))
        {
            return (false, "Phone number already exists.", null);
        }

        // 5. Role assignment enforcement
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        string? assignedUserId = model.AssignedToUserId;

        if (!isPrivileged)
        {
            // SalesExecutive can only assign to themselves
            assignedUserId = currentUserId;
        }
        else if (string.IsNullOrEmpty(assignedUserId))
        {
            assignedUserId = currentUserId;
        }

        var currentUser = await _userManager.FindByIdAsync(currentUserId);
        var createdByName = currentUser?.FullName ?? currentUser?.UserName ?? "User";

        var customer = new Customer
        {
            CustomerCode = model.CustomerCode.Trim(),
            CustomerName = model.CustomerName.Trim(),
            Email = model.Email.Trim().ToLower(),
            Phone = model.Phone.Trim(),
            CompanyName = model.CompanyName?.Trim(),
            Address = model.Address?.Trim(),
            City = model.City?.Trim(),
            State = model.State?.Trim(),
            PostalCode = model.PostalCode?.Trim(),
            Country = model.Country?.Trim(),
            Industry = model.Industry?.Trim(),
            Status = model.Status,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = createdByName,
            AssignedToUserId = assignedUserId
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        // 6. Record Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "CreateCustomer",
            EntityName = "Customer",
            EntityId = customer.Id.ToString(),
            Details = $"Created customer '{customer.CustomerName}' (Code: {customer.CustomerCode}, Email: {customer.Email}, Phone: {customer.Phone}).",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Customer {CustomerCode} created by user {UserId}.", customer.CustomerCode, currentUserId);
        return (true, null, customer);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateCustomerAsync(
        CustomerEditViewModel model,
        string currentUserId,
        IList<string> roles,
        string? ipAddress)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == model.Id);
        if (customer == null)
        {
            return (false, "Customer not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Authorization check: SalesExecutive cannot edit another user's customer
        if (!isPrivileged && customer.AssignedToUserId != currentUserId)
        {
            _logger.LogWarning("Forbidden: User {UserId} attempted to update unauthorized Customer {CustomerId}", currentUserId, model.Id);
            return (false, "You are not authorized to edit this customer.");
        }

        // Validate Customer Name
        if (string.IsNullOrWhiteSpace(model.CustomerName))
        {
            return (false, "Customer Name is required.");
        }

        // Validate unique email
        if (!await IsEmailUniqueAsync(model.Email, model.Id))
        {
            return (false, "Email already exists.");
        }

        // Validate unique phone
        if (!await IsPhoneUniqueAsync(model.Phone, model.Id))
        {
            return (false, "Phone number already exists.");
        }

        // Validate customer code
        if (!await IsCustomerCodeUniqueAsync(model.CustomerCode, model.Id))
        {
            return (false, "Customer code already exists.");
        }

        // Apply changes
        customer.CustomerCode = model.CustomerCode.Trim();
        customer.CustomerName = model.CustomerName.Trim();
        customer.Email = model.Email.Trim().ToLower();
        customer.Phone = model.Phone.Trim();
        customer.CompanyName = model.CompanyName?.Trim();
        customer.Address = model.Address?.Trim();
        customer.City = model.City?.Trim();
        customer.State = model.State?.Trim();
        customer.PostalCode = model.PostalCode?.Trim();
        customer.Country = model.Country?.Trim();
        customer.Industry = model.Industry?.Trim();
        customer.Status = model.Status;
        customer.UpdatedAt = DateTime.UtcNow;

        if (isPrivileged && !string.IsNullOrEmpty(model.AssignedToUserId))
        {
            customer.AssignedToUserId = model.AssignedToUserId;
        }

        await _context.SaveChangesAsync();

        // Record Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "UpdateCustomer",
            EntityName = "Customer",
            EntityId = customer.Id.ToString(),
            Details = $"Updated customer '{customer.CustomerName}' (Code: {customer.CustomerCode}, Status: {customer.Status}).",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Customer {CustomerId} updated by user {UserId}.", customer.Id, currentUserId);
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeactivateCustomerAsync(
        int id,
        string currentUserId,
        IList<string> roles,
        string? ipAddress)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null)
        {
            return (false, "Customer not found.");
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Authorization check
        if (!isPrivileged && customer.AssignedToUserId != currentUserId)
        {
            _logger.LogWarning("Forbidden: User {UserId} attempted to deactivate unauthorized Customer {CustomerId}", currentUserId, id);
            return (false, "You are not authorized to deactivate this customer.");
        }

        // Soft deactivation: set Status to Inactive
        customer.Status = CustomerStatus.Inactive;
        customer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Record Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "DeactivateCustomer",
            EntityName = "Customer",
            EntityId = customer.Id.ToString(),
            Details = $"Deactivated customer '{customer.CustomerName}' (Code: {customer.CustomerCode}). Status changed to Inactive.",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Customer {CustomerId} deactivated by user {UserId}.", id, currentUserId);
        return (true, null);
    }

    public async Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return true;
        var normalizedEmail = email.Trim().ToLower();

        var query = _context.Customers.Where(c => c.Email.ToLower() == normalizedEmail);
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<bool> IsPhoneUniqueAsync(string phone, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(phone)) return true;
        var normalizedPhone = phone.Trim();

        var query = _context.Customers.Where(c => c.Phone == normalizedPhone);
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<bool> IsCustomerCodeUniqueAsync(string customerCode, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(customerCode)) return true;
        var normalizedCode = customerCode.Trim().ToUpper();

        var query = _context.Customers.Where(c => c.CustomerCode.ToUpper() == normalizedCode);
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<string> GenerateCustomerCodeAsync()
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
}
