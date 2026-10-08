using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Customers;

namespace AcxiomCRM.Services;

public interface ICustomerService
{
    Task<CustomerListViewModel> GetPagedCustomersAsync(CustomerFilterViewModel filter, string currentUserId, IList<string> roles);
    Task<Customer?> GetCustomerByIdAsync(int id, string currentUserId, IList<string> roles);
    Task<CustomerDetailsViewModel?> GetCustomerDetailsAsync(int id, string currentUserId, IList<string> roles);
    Task<CustomerEditViewModel?> GetCustomerForEditAsync(int id, string currentUserId, IList<string> roles);
    Task<CustomerDeleteViewModel?> GetCustomerForDeleteAsync(int id, string currentUserId, IList<string> roles);
    Task<(bool Success, string? ErrorMessage, Customer? Customer)> CreateCustomerAsync(CustomerCreateViewModel model, string currentUserId, IList<string> roles, string? ipAddress);
    Task<(bool Success, string? ErrorMessage)> UpdateCustomerAsync(CustomerEditViewModel model, string currentUserId, IList<string> roles, string? ipAddress);
    Task<(bool Success, string? ErrorMessage)> DeactivateCustomerAsync(int id, string currentUserId, IList<string> roles, string? ipAddress);
    Task<bool> CanUserAccessCustomerAsync(int customerId, string currentUserId, IList<string> roles);
    Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null);
    Task<bool> IsPhoneUniqueAsync(string phone, int? excludeId = null);
    Task<bool> IsCustomerCodeUniqueAsync(string customerCode, int? excludeId = null);
    Task<string> GenerateCustomerCodeAsync();
}
