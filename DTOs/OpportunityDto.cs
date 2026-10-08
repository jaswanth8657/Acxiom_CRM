using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;

namespace AcxiomCRM.DTOs;

public class OpportunityDto
{
    public int Id { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public decimal Amount { get; set; }
    public string Stage { get; set; } = string.Empty;
    public int Probability { get; set; }
    public decimal WeightedAmount { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedDate { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public string? Notes { get; set; }
}

public class CreateOpportunityDto
{
    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
    public string OpportunityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "CustomerId is required.")]
    public int CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, 1000000000, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 10;

    [Required(ErrorMessage = "Expected Close Date is required.")]
    public DateTime ExpectedCloseDate { get; set; }

    public string? AssignedToUserId { get; set; }

    [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
    public string? Notes { get; set; }
}

public class UpdateOpportunityDto
{
    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
    public string OpportunityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "CustomerId is required.")]
    public int CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, 1000000000, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required]
    public OpportunityStage Stage { get; set; }

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; }

    [Required(ErrorMessage = "Expected Close Date is required.")]
    public DateTime ExpectedCloseDate { get; set; }

    public OpportunityStatus Status { get; set; } = OpportunityStatus.Active;

    public string? AssignedToUserId { get; set; }

    [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
    public string? Notes { get; set; }
}

public class OpportunityQueryDto
{
    public string? SearchTerm { get; set; }
    public OpportunityStage? Stage { get; set; }
    public OpportunityStatus? Status { get; set; }
    public string? AssignedToUserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
