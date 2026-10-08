using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ApplicationUser configuration
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            entity.HasIndex(u => u.Email);
        });

        // Customer configuration
        builder.Entity<Customer>(entity =>
        {
            entity.HasIndex(c => c.CustomerCode).IsUnique();
            entity.HasIndex(c => c.Email).IsUnique();
            entity.HasIndex(c => c.Phone).IsUnique();
            entity.HasIndex(c => c.CustomerName);
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.AssignedToUserId);

            entity.HasOne(c => c.AssignedToUser)
                .WithMany(u => u.AssignedCustomers)
                .HasForeignKey(c => c.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Lead configuration
        builder.Entity<Lead>(entity =>
        {
            entity.HasIndex(l => l.LeadCode).IsUnique();
            entity.HasIndex(l => l.Email);
            entity.HasIndex(l => l.Phone);
            entity.HasIndex(l => l.LeadName);
            entity.HasIndex(l => l.Status);
            entity.HasIndex(l => l.Source);
            entity.HasIndex(l => l.AssignedToUserId);

            entity.HasOne(l => l.AssignedToUser)
                .WithMany(u => u.AssignedLeads)
                .HasForeignKey(l => l.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(l => l.ConvertedCustomer)
                .WithMany()
                .HasForeignKey(l => l.ConvertedCustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(l => l.ConvertedOpportunity)
                .WithMany()
                .HasForeignKey(l => l.ConvertedOpportunityId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Opportunity configuration
        builder.Entity<Opportunity>(entity =>
        {
            entity.HasIndex(o => o.OpportunityName);
            entity.HasIndex(o => o.CustomerId);
            entity.HasIndex(o => o.AssignedToUserId);
            entity.HasIndex(o => o.LeadId);
            entity.HasIndex(o => o.Stage);
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.ExpectedCloseDate);

            entity.HasOne(o => o.Customer)
                .WithMany(c => c.Opportunities)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(o => o.AssignedToUser)
                .WithMany(u => u.AssignedOpportunities)
                .HasForeignKey(o => o.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(o => o.Lead)
                .WithMany()
                .HasForeignKey(o => o.LeadId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // FollowUp configuration
        builder.Entity<FollowUp>(entity =>
        {
            entity.HasIndex(f => f.ScheduledDate);
            entity.HasIndex(f => f.Status);
            entity.HasIndex(f => f.Type);
            entity.HasIndex(f => f.AssignedToUserId);
            entity.HasIndex(f => f.CustomerId);
            entity.HasIndex(f => f.LeadId);
            entity.HasIndex(f => f.OpportunityId);

            entity.HasOne(f => f.Customer)
                .WithMany(c => c.FollowUps)
                .HasForeignKey(f => f.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.Lead)
                .WithMany(l => l.FollowUps)
                .HasForeignKey(f => f.LeadId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.Opportunity)
                .WithMany(o => o.FollowUps)
                .HasForeignKey(f => f.OpportunityId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.AssignedToUser)
                .WithMany(u => u.AssignedFollowUps)
                .HasForeignKey(f => f.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Activity configuration
        builder.Entity<Activity>(entity =>
        {
            entity.HasIndex(a => a.ActivityDate);
            entity.HasIndex(a => a.Status);
            entity.HasIndex(a => a.Type);
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.CustomerId);
            entity.HasIndex(a => a.LeadId);
            entity.HasIndex(a => a.OpportunityId);

            entity.HasOne(a => a.Customer)
                .WithMany(c => c.Activities)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Lead)
                .WithMany(l => l.Activities)
                .HasForeignKey(a => a.LeadId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Opportunity)
                .WithMany(o => o.Activities)
                .HasForeignKey(a => a.OpportunityId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.User)
                .WithMany(u => u.Activities)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog configuration
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => a.Timestamp);
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.Action);

            entity.HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
