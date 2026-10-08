using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class AuditLog
{
    public int Id { get; set; }

    public string? UserId { get; set; }
    public virtual ApplicationUser? User { get; set; }

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? EntityId { get; set; }

    [StringLength(4000)]
    public string? Details { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
