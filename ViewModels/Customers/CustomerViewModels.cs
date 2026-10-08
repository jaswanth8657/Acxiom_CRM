using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels.Customers;

public class CustomerFilterViewModel
{
    public string? SearchTerm { get; set; }
    public CustomerStatus? Status { get; set; }
    public string? AssignedToUserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class CustomerListItemViewModel
{
    public int Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public CustomerStatus Status { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CustomerListViewModel
{
    public List<CustomerListItemViewModel> Customers { get; set; } = new();
    public CustomerFilterViewModel Filter { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / (Filter.PageSize > 0 ? Filter.PageSize : 10));
    public int CurrentPage => Filter.Page;
    public int PageSize => Filter.PageSize;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public bool CanManageAll { get; set; }
}

public class CustomerCreateViewModel
{
    [Display(Name = "Customer Code")]
    [StringLength(50)]
    public string? CustomerCode { get; set; }

    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(100, ErrorMessage = "Customer Name cannot exceed 100 characters.")]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a valid email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a valid phone number.")]
    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [RegularExpression(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$", ErrorMessage = "Enter a valid phone number.")]
    [StringLength(30, ErrorMessage = "Phone cannot exceed 30 characters.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [StringLength(250, ErrorMessage = "Address cannot exceed 250 characters.")]
    public string? Address { get; set; }

    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    [StringLength(100, ErrorMessage = "State cannot exceed 100 characters.")]
    public string? State { get; set; }

    [StringLength(20, ErrorMessage = "Postal Code cannot exceed 20 characters.")]
    [Display(Name = "Postal Code")]
    public string? PostalCode { get; set; }

    [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters.")]
    public string? Country { get; set; }

    [StringLength(100, ErrorMessage = "Industry cannot exceed 100 characters.")]
    public string? Industry { get; set; }

    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    [Display(Name = "Assigned Sales Executive")]
    public string? AssignedToUserId { get; set; }

    public List<SelectListItem> AvailableUsers { get; set; } = new();
}

public class CustomerEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Customer Code is required.")]
    [StringLength(50)]
    [Display(Name = "Customer Code")]
    public string CustomerCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(100, ErrorMessage = "Customer Name cannot exceed 100 characters.")]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a valid email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a valid phone number.")]
    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [RegularExpression(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$", ErrorMessage = "Enter a valid phone number.")]
    [StringLength(30, ErrorMessage = "Phone cannot exceed 30 characters.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [StringLength(250, ErrorMessage = "Address cannot exceed 250 characters.")]
    public string? Address { get; set; }

    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    [StringLength(100, ErrorMessage = "State cannot exceed 100 characters.")]
    public string? State { get; set; }

    [StringLength(20, ErrorMessage = "Postal Code cannot exceed 20 characters.")]
    [Display(Name = "Postal Code")]
    public string? PostalCode { get; set; }

    [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters.")]
    public string? Country { get; set; }

    [StringLength(100, ErrorMessage = "Industry cannot exceed 100 characters.")]
    public string? Industry { get; set; }

    public CustomerStatus Status { get; set; }

    [Display(Name = "Assigned Sales Executive")]
    public string? AssignedToUserId { get; set; }

    public List<SelectListItem> AvailableUsers { get; set; } = new();
}

public class CustomerDetailsViewModel
{
    public int Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? Industry { get; set; }
    public CustomerStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }

    public List<CustomerActivityItemViewModel> Activities { get; set; } = new();
    public List<CustomerOpportunityItemViewModel> Opportunities { get; set; } = new();
    public List<CustomerFollowUpItemViewModel> FollowUps { get; set; } = new();
}

public class CustomerActivityItemViewModel
{
    public int Id { get; set; }
    public ActivityType Type { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ActivityDate { get; set; }
    public string UserName { get; set; } = string.Empty;
}

public class CustomerOpportunityItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public OpportunityStage Stage { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
}

public class CustomerFollowUpItemViewModel
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public FollowUpStatus Status { get; set; }
    public FollowUpPriority Priority { get; set; }
}

public class CustomerDeleteViewModel
{
    public int Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public CustomerStatus Status { get; set; }
    public string? AssignedToUserName { get; set; }
}
