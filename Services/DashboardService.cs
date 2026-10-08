using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Activities;
using AcxiomCRM.ViewModels.Dashboard;
using AcxiomCRM.ViewModels.FollowUps;

namespace AcxiomCRM.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<DashboardViewModel> GetDashboardMetricsAsync(
        DashboardFilterViewModel filter,
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        var currentUser = await _userManager.FindByIdAsync(currentUserId);
        var primaryRole = roles.FirstOrDefault() ?? "SalesExecutive";

        // Determine date filter boundaries
        DateTime? filterStart = null;
        DateTime? filterEnd = null;
        var today = DateTime.Today;

        switch (filter.Period)
        {
            case DateFilterPeriod.Today:
                filterStart = today;
                filterEnd = today.AddDays(1).AddTicks(-1);
                break;
            case DateFilterPeriod.ThisWeek:
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                filterStart = today.AddDays(-1 * diff);
                filterEnd = filterStart.Value.AddDays(7).AddTicks(-1);
                break;
            case DateFilterPeriod.ThisMonth:
                filterStart = new DateTime(today.Year, today.Month, 1);
                filterEnd = filterStart.Value.AddMonths(1).AddTicks(-1);
                break;
            case DateFilterPeriod.Custom:
                if (filter.StartDate.HasValue) filterStart = filter.StartDate.Value.Date;
                if (filter.EndDate.HasValue) filterEnd = filter.EndDate.Value.Date.AddDays(1).AddTicks(-1);
                break;
        }

        // 1. Scoped Customers
        var custQuery = _context.Customers.AsNoTracking();
        if (!isPrivileged) custQuery = custQuery.Where(c => c.AssignedToUserId == currentUserId);
        if (filterStart.HasValue) custQuery = custQuery.Where(c => c.CreatedDate >= filterStart.Value);
        if (filterEnd.HasValue) custQuery = custQuery.Where(c => c.CreatedDate <= filterEnd.Value);

        var totalCustomers = await custQuery.CountAsync();

        // 2. Scoped Leads
        var leadQuery = _context.Leads.AsNoTracking();
        if (!isPrivileged) leadQuery = leadQuery.Where(l => l.AssignedToUserId == currentUserId);
        if (filterStart.HasValue) leadQuery = leadQuery.Where(l => l.CreatedDate >= filterStart.Value);
        if (filterEnd.HasValue) leadQuery = leadQuery.Where(l => l.CreatedDate <= filterEnd.Value);

        var totalLeads = await leadQuery.CountAsync();
        var openLeads = await leadQuery.CountAsync(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost);

        // 3. Scoped Opportunities
        var oppQuery = _context.Opportunities.AsNoTracking();
        if (!isPrivileged) oppQuery = oppQuery.Where(o => o.AssignedToUserId == currentUserId);
        if (filterStart.HasValue) oppQuery = oppQuery.Where(o => o.CreatedDate >= filterStart.Value);
        if (filterEnd.HasValue) oppQuery = oppQuery.Where(o => o.CreatedDate <= filterEnd.Value);

        var totalOpportunities = await oppQuery.CountAsync();
        var openOppsQuery = oppQuery.Where(o => o.Status == OpportunityStatus.Active && o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost);
        var openOpportunities = await openOppsQuery.CountAsync();
        var wonOpportunities = await oppQuery.CountAsync(o => o.Status == OpportunityStatus.Active && o.Stage == OpportunityStage.Won);
        var lostOpportunities = await oppQuery.CountAsync(o => o.Status == OpportunityStatus.Active && o.Stage == OpportunityStage.Lost);

        var openDealsList = await openOppsQuery.Select(o => new { o.Amount, o.Probability }).ToListAsync();
        var totalPipelineValue = openDealsList.Sum(d => d.Amount);
        var weightedPipelineValue = openDealsList.Sum(d => Math.Round(d.Amount * d.Probability / 100m, 2));

        // 4. Scoped Follow-Ups
        var fuQuery = _context.FollowUps.AsNoTracking();
        if (!isPrivileged) fuQuery = fuQuery.Where(f => f.AssignedToUserId == currentUserId);
        if (filterStart.HasValue) fuQuery = fuQuery.Where(f => f.ScheduledDate >= filterStart.Value);
        if (filterEnd.HasValue) fuQuery = fuQuery.Where(f => f.ScheduledDate <= filterEnd.Value);

        var pendingFollowUps = await fuQuery.CountAsync(f => f.Status == FollowUpStatus.Planned && f.ScheduledDate >= today);
        var overdueFollowUps = await fuQuery.CountAsync(f => (f.Status == FollowUpStatus.Planned && f.ScheduledDate < today) || f.Status == FollowUpStatus.Missed);

        // 5. Chart Data
        var leadChart = await GetLeadStatusChartDataAsync(currentUserId, roles, filterStart, filterEnd);
        var oppChart = await GetOpportunityPipelineChartDataAsync(currentUserId, roles, filterStart, filterEnd);
        var salesChart = await GetMonthlySalesChartDataAsync(currentUserId, roles);

        // 6. Recent Follow-Ups
        var recentFollowUps = await _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.AssignedToUser)
            .AsNoTracking()
            .Where(f => isPrivileged || f.AssignedToUserId == currentUserId)
            .OrderByDescending(f => f.ScheduledDate)
            .Take(5)
            .Select(f => new FollowUpListItemViewModel
            {
                Id = f.Id,
                Subject = f.Subject,
                ScheduledDate = f.ScheduledDate,
                Type = f.Type,
                Status = f.Status,
                Priority = f.Priority,
                CustomerName = f.Customer != null ? f.Customer.CustomerName : null,
                LeadName = f.Lead != null ? f.Lead.LeadName : null,
                AssignedToUserName = f.AssignedToUser != null ? f.AssignedToUser.FullName : "Unassigned"
            })
            .ToListAsync();

        // 7. Recent Activities
        var recentActivities = await _context.Activities
            .Include(a => a.User)
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .AsNoTracking()
            .Where(a => isPrivileged || a.UserId == currentUserId || (a.Customer != null && a.Customer.AssignedToUserId == currentUserId))
            .OrderByDescending(a => a.ActivityDate)
            .Take(5)
            .Select(a => new ActivityListItemViewModel
            {
                Id = a.Id,
                Subject = a.Subject,
                Type = a.Type,
                Status = a.Status,
                ActivityDate = a.ActivityDate,
                CustomerName = a.Customer != null ? a.Customer.CustomerName : null,
                LeadName = a.Lead != null ? a.Lead.LeadName : null,
                UserName = a.User != null ? a.User.FullName : "Unknown"
            })
            .ToListAsync();

        return new DashboardViewModel
        {
            CurrentUserId = currentUserId,
            FullName = currentUser?.FullName ?? "User",
            UserRole = primaryRole,
            Roles = roles,
            Filter = filter,
            TotalCustomers = totalCustomers,
            TotalLeads = totalLeads,
            OpenLeads = openLeads,
            TotalOpportunities = totalOpportunities,
            OpenOpportunities = openOpportunities,
            WonOpportunities = wonOpportunities,
            LostOpportunities = lostOpportunities,
            TotalPipelineValue = totalPipelineValue,
            WeightedPipelineValue = weightedPipelineValue,
            PendingFollowUps = pendingFollowUps,
            OverdueFollowUps = overdueFollowUps,
            LeadStatusChart = leadChart,
            OpportunityPipelineChart = oppChart,
            MonthlySalesChart = salesChart,
            RecentFollowUps = recentFollowUps,
            RecentActivities = recentActivities
        };
    }

    public async Task<LeadStatusChartData> GetLeadStatusChartDataAsync(
        string currentUserId,
        IList<string> roles,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var query = _context.Leads.AsNoTracking();
        if (!isPrivileged) query = query.Where(l => l.AssignedToUserId == currentUserId);
        if (startDate.HasValue) query = query.Where(l => l.CreatedDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(l => l.CreatedDate <= endDate.Value);

        var statuses = await query.Select(l => l.Status).ToListAsync();
        var grouped = statuses
            .GroupBy(s => s)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToList();

        var dict = grouped.ToDictionary(g => g.Status, g => g.Count);

        return new LeadStatusChartData
        {
            Labels = new List<string> { "New", "Contacted", "Qualified", "Lost", "Converted" },
            Counts = new List<int>
            {
                dict.GetValueOrDefault(LeadStatus.New, 0),
                dict.GetValueOrDefault(LeadStatus.Contacted, 0),
                dict.GetValueOrDefault(LeadStatus.Qualified, 0),
                dict.GetValueOrDefault(LeadStatus.Lost, 0),
                dict.GetValueOrDefault(LeadStatus.Converted, 0)
            }
        };
    }

    public async Task<OpportunityPipelineChartData> GetOpportunityPipelineChartDataAsync(
        string currentUserId,
        IList<string> roles,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var query = _context.Opportunities.AsNoTracking().Where(o => o.Status == OpportunityStatus.Active);
        if (!isPrivileged) query = query.Where(o => o.AssignedToUserId == currentUserId);
        if (startDate.HasValue) query = query.Where(o => o.CreatedDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(o => o.CreatedDate <= endDate.Value);

        var oppItems = await query.Select(o => new { o.Stage, o.Amount }).ToListAsync();
        var grouped = oppItems
            .GroupBy(o => o.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count(), TotalAmount = g.Sum(x => x.Amount) })
            .ToList();

        var stageMap = grouped.ToDictionary(g => g.Stage, g => (g.Count, g.TotalAmount));

        var stages = new[]
        {
            OpportunityStage.Qualification,
            OpportunityStage.Proposal,
            OpportunityStage.Negotiation,
            OpportunityStage.Won,
            OpportunityStage.Lost
        };

        var counts = new List<int>();
        var amounts = new List<decimal>();

        foreach (var stage in stages)
        {
            if (stageMap.TryGetValue(stage, out var val))
            {
                counts.Add(val.Count);
                amounts.Add(val.TotalAmount);
            }
            else
            {
                counts.Add(0);
                amounts.Add(0);
            }
        }

        return new OpportunityPipelineChartData
        {
            Labels = new List<string> { "Qualification", "Proposal", "Negotiation", "Won", "Lost" },
            Counts = counts,
            Amounts = amounts
        };
    }

    public async Task<MonthlySalesChartData> GetMonthlySalesChartDataAsync(
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var query = _context.Opportunities
            .AsNoTracking()
            .Where(o => o.Status == OpportunityStatus.Active && o.Stage == OpportunityStage.Won);

        if (!isPrivileged) query = query.Where(o => o.AssignedToUserId == currentUserId);

        var wonDeals = await query
            .Select(o => new { o.Amount, Date = o.ExpectedCloseDate })
            .ToListAsync();

        var months = new List<string>();
        var wonAmounts = new List<decimal>();
        var wonCounts = new List<int>();

        // Last 6 calendar months
        for (int i = 5; i >= 0; i--)
        {
            var targetMonth = DateTime.Today.AddMonths(-i);
            var monthLabel = targetMonth.ToString("MMM yyyy", CultureInfo.InvariantCulture);
            months.Add(monthLabel);

            var inMonth = wonDeals.Where(d => d.Date.Year == targetMonth.Year && d.Date.Month == targetMonth.Month).ToList();
            wonAmounts.Add(inMonth.Sum(d => d.Amount));
            wonCounts.Add(inMonth.Count);
        }

        return new MonthlySalesChartData
        {
            Months = months,
            WonAmounts = wonAmounts,
            WonCounts = wonCounts
        };
    }
}
