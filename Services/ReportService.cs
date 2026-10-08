using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Reports;

namespace AcxiomCRM.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private static string EscapeCsv(string? val)
    {
        if (string.IsNullOrEmpty(val)) return "\"\"";
        var escaped = val.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    #region 1. Customer Report
    public async Task<CustomerReportViewModel> GetCustomerReportAsync(
        CustomerReportViewModel filter,
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        var query = _context.Customers
            .Include(c => c.AssignedToUser)
            .AsNoTracking();

        if (!isPrivileged)
        {
            query = query.Where(c => c.AssignedToUserId == currentUserId);
        }
        else if (!string.IsNullOrEmpty(filter.AssignedToUserId))
        {
            query = query.Where(c => c.AssignedToUserId == filter.AssignedToUserId);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(c => c.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(c =>
                c.CustomerName.ToLower().Contains(term) ||
                c.CustomerCode.ToLower().Contains(term) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)) ||
                c.Email.ToLower().Contains(term));
        }

        filter.TotalItems = await query.CountAsync();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 15 : filter.PageSize;

        filter.Items = await query
            .OrderByDescending(c => c.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerReportItem
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Status = c.Status,
                AssignedToUserName = c.AssignedToUser != null ? c.AssignedToUser.FullName : "Unassigned",
                CreatedDate = c.CreatedDate
            })
            .ToListAsync();

        filter.CanFilterUsers = isPrivileged;
        if (isPrivileged)
        {
            filter.AvailableUsers = await GetUserSelectListAsync(filter.AssignedToUserId);
        }

        return filter;
    }

    public async Task<byte[]> ExportCustomerReportCsvAsync(CustomerReportViewModel filter, string currentUserId, IList<string> roles)
    {
        filter.Page = 1;
        filter.PageSize = 10000;
        var report = await GetCustomerReportAsync(filter, currentUserId, roles);

        var sb = new StringBuilder();
        sb.AppendLine("Customer Code,Customer Name,Company,Email,Phone,Status,Owner,Created Date");
        foreach (var c in report.Items)
        {
            sb.AppendLine($"{EscapeCsv(c.CustomerCode)},{EscapeCsv(c.CustomerName)},{EscapeCsv(c.CompanyName)},{EscapeCsv(c.Email)},{EscapeCsv(c.Phone)},{EscapeCsv(c.Status.ToString())},{EscapeCsv(c.AssignedToUserName)},{EscapeCsv(c.CreatedDate.ToString("yyyy-MM-dd"))}");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
    #endregion

    #region 2. Lead Report
    public async Task<LeadReportViewModel> GetLeadReportAsync(
        LeadReportViewModel filter,
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        var query = _context.Leads
            .Include(l => l.AssignedToUser)
            .Include(l => l.ConvertedCustomer)
            .AsNoTracking();

        if (!isPrivileged)
        {
            query = query.Where(l => l.AssignedToUserId == currentUserId);
        }
        else if (!string.IsNullOrEmpty(filter.AssignedToUserId))
        {
            query = query.Where(l => l.AssignedToUserId == filter.AssignedToUserId);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(l => l.Status == filter.Status.Value);
        }

        if (!string.IsNullOrEmpty(filter.Source))
        {
            query = query.Where(l => l.Source == filter.Source);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(l =>
                l.LeadName.ToLower().Contains(term) ||
                l.LeadCode.ToLower().Contains(term) ||
                (l.CompanyName != null && l.CompanyName.ToLower().Contains(term)) ||
                l.Email.ToLower().Contains(term));
        }

        filter.TotalItems = await query.CountAsync();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 15 : filter.PageSize;

        filter.Items = await query
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LeadReportItem
            {
                Id = l.Id,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                ExpectedValue = l.ExpectedValue ?? 0m,
                AssignedToUserName = l.AssignedToUser != null ? l.AssignedToUser.FullName : "Unassigned",
                CreatedDate = l.CreatedDate,
                ConvertedCustomerId = l.ConvertedCustomerId,
                ConvertedCustomerName = l.ConvertedCustomer != null ? l.ConvertedCustomer.CustomerName : null
            })
            .ToListAsync();

        filter.CanFilterUsers = isPrivileged;
        if (isPrivileged)
        {
            filter.AvailableUsers = await GetUserSelectListAsync(filter.AssignedToUserId);
        }

        filter.AvailableSources = LeadSources.All.Select(s => new SelectListItem
        {
            Value = s,
            Text = s,
            Selected = filter.Source == s
        }).ToList();

        return filter;
    }

    public async Task<byte[]> ExportLeadReportCsvAsync(LeadReportViewModel filter, string currentUserId, IList<string> roles)
    {
        filter.Page = 1;
        filter.PageSize = 10000;
        var report = await GetLeadReportAsync(filter, currentUserId, roles);

        var sb = new StringBuilder();
        sb.AppendLine("Lead Code,Lead Name,Company,Source,Status,Expected Value,Owner,Converted Account,Created Date");
        foreach (var l in report.Items)
        {
            sb.AppendLine($"{EscapeCsv(l.LeadCode)},{EscapeCsv(l.LeadName)},{EscapeCsv(l.CompanyName)},{EscapeCsv(l.Source)},{EscapeCsv(l.Status.ToString())},{l.ExpectedValue:F2},{EscapeCsv(l.AssignedToUserName)},{EscapeCsv(l.ConvertedCustomerName ?? "Not Converted")},{EscapeCsv(l.CreatedDate.ToString("yyyy-MM-dd"))}");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
    #endregion

    #region 3. Follow-Up Report
    public async Task<FollowUpReportViewModel> GetFollowUpReportAsync(
        FollowUpReportViewModel filter,
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

        if (!isPrivileged)
        {
            query = query.Where(f => f.AssignedToUserId == currentUserId);
        }
        else if (!string.IsNullOrEmpty(filter.AssignedToUserId))
        {
            query = query.Where(f => f.AssignedToUserId == filter.AssignedToUserId);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(f => f.Status == filter.Status.Value);
        }

        if (filter.Type.HasValue)
        {
            query = query.Where(f => f.Type == filter.Type.Value);
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(f => f.ScheduledDate >= filter.StartDate.Value.Date);
        }
        if (filter.EndDate.HasValue)
        {
            query = query.Where(f => f.ScheduledDate <= filter.EndDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(f =>
                f.Subject.ToLower().Contains(term) ||
                (f.Notes != null && f.Notes.ToLower().Contains(term)));
        }

        filter.TotalItems = await query.CountAsync();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 15 : filter.PageSize;

        filter.Items = await query
            .OrderByDescending(f => f.ScheduledDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FollowUpReportItem
            {
                Id = f.Id,
                Subject = f.Subject,
                Type = f.Type,
                Status = f.Status,
                ScheduledDate = f.ScheduledDate,
                RelatedRecordName = f.Customer != null ? f.Customer.CustomerName : (f.Lead != null ? f.Lead.LeadName : (f.Opportunity != null ? f.Opportunity.OpportunityName : "None")),
                AssignedToUserName = f.AssignedToUser != null ? f.AssignedToUser.FullName : "Unassigned",
                CreatedAt = f.CreatedAt,
                CompletedAt = f.CompletedAt
            })
            .ToListAsync();

        filter.CanFilterUsers = isPrivileged;
        if (isPrivileged)
        {
            filter.AvailableUsers = await GetUserSelectListAsync(filter.AssignedToUserId);
        }

        return filter;
    }

    public async Task<byte[]> ExportFollowUpReportCsvAsync(FollowUpReportViewModel filter, string currentUserId, IList<string> roles)
    {
        filter.Page = 1;
        filter.PageSize = 10000;
        var report = await GetFollowUpReportAsync(filter, currentUserId, roles);

        var sb = new StringBuilder();
        sb.AppendLine("Task Subject,Type,Status,Scheduled Date,Overdue,Related Record,Owner,Completed Date");
        foreach (var f in report.Items)
        {
            sb.AppendLine($"{EscapeCsv(f.Subject)},{EscapeCsv(f.Type.ToString())},{EscapeCsv(f.Status.ToString())},{EscapeCsv(f.ScheduledDate.ToString("yyyy-MM-dd HH:mm"))},{f.IsOverdue},{EscapeCsv(f.RelatedRecordName)},{EscapeCsv(f.AssignedToUserName)},{EscapeCsv(f.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "Pending")}");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
    #endregion

    #region 4. Opportunity Report
    public async Task<OpportunityReportViewModel> GetOpportunityReportAsync(
        OpportunityReportViewModel filter,
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        var query = _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.AssignedToUser)
            .AsNoTracking();

        if (!isPrivileged)
        {
            query = query.Where(o => o.AssignedToUserId == currentUserId);
        }
        else if (!string.IsNullOrEmpty(filter.AssignedToUserId))
        {
            query = query.Where(o => o.AssignedToUserId == filter.AssignedToUserId);
        }

        if (filter.Stage.HasValue)
        {
            query = query.Where(o => o.Stage == filter.Stage.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(o => o.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(o =>
                o.OpportunityName.ToLower().Contains(term) ||
                (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(term)));
        }

        filter.TotalItems = await query.CountAsync();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 15 : filter.PageSize;

        filter.Items = await query
            .OrderByDescending(o => o.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OpportunityReportItem
            {
                Id = o.Id,
                OpportunityName = o.OpportunityName,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : "Unknown",
                Stage = o.Stage,
                Amount = o.Amount,
                Probability = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                AssignedToUserName = o.AssignedToUser != null ? o.AssignedToUser.FullName : "Unassigned",
                CreatedDate = o.CreatedDate
            })
            .ToListAsync();

        filter.CanFilterUsers = isPrivileged;
        if (isPrivileged)
        {
            filter.AvailableUsers = await GetUserSelectListAsync(filter.AssignedToUserId);
        }

        return filter;
    }

    public async Task<byte[]> ExportOpportunityReportCsvAsync(OpportunityReportViewModel filter, string currentUserId, IList<string> roles)
    {
        filter.Page = 1;
        filter.PageSize = 10000;
        var report = await GetOpportunityReportAsync(filter, currentUserId, roles);

        var sb = new StringBuilder();
        sb.AppendLine("Opportunity Name,Customer,Stage,Amount,Probability (%),Weighted Amount,Close Date,Status,Owner");
        foreach (var o in report.Items)
        {
            sb.AppendLine($"{EscapeCsv(o.OpportunityName)},{EscapeCsv(o.CustomerName)},{EscapeCsv(o.Stage.ToString())},{o.Amount:F2},{o.Probability},{o.WeightedAmount:F2},{EscapeCsv(o.ExpectedCloseDate.ToString("yyyy-MM-dd"))},{EscapeCsv(o.Status.ToString())},{EscapeCsv(o.AssignedToUserName)}");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
    #endregion

    #region 5. Pipeline Report
    public async Task<PipelineReportViewModel> GetPipelineReportAsync(string currentUserId, IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var query = _context.Opportunities
            .Include(o => o.AssignedToUser)
            .AsNoTracking()
            .Where(o => o.Status == OpportunityStatus.Active && o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost);

        if (!isPrivileged)
        {
            query = query.Where(o => o.AssignedToUserId == currentUserId);
        }

        var openDeals = await query.ToListAsync();
        var totalPipeline = openDeals.Sum(d => d.Amount);
        var totalWeighted = openDeals.Sum(d => Math.Round(d.Amount * d.Probability / 100m, 2));

        // Group by Stage
        var stages = new[] { OpportunityStage.Prospecting, OpportunityStage.Qualification, OpportunityStage.Proposal, OpportunityStage.Negotiation };
        var stageBreakdowns = new List<StagePipelineSummary>();

        foreach (var stage in stages)
        {
            var stageDeals = openDeals.Where(d => d.Stage == stage).ToList();
            var stageTotal = stageDeals.Sum(d => d.Amount);
            var stageWeighted = stageDeals.Sum(d => Math.Round(d.Amount * d.Probability / 100m, 2));
            var share = totalPipeline > 0 ? Math.Round(stageTotal / totalPipeline * 100m, 1) : 0;

            stageBreakdowns.Add(new StagePipelineSummary
            {
                Stage = stage,
                DealCount = stageDeals.Count,
                TotalAmount = stageTotal,
                WeightedAmount = stageWeighted,
                SharePercentage = share
            });
        }

        // Group by Owner
        var ownerGroups = openDeals
            .GroupBy(d => d.AssignedToUserId)
            .Select(g => new OwnerPipelineSummary
            {
                UserId = g.Key ?? string.Empty,
                UserName = g.First().AssignedToUser?.FullName ?? "Unassigned",
                DealCount = g.Count(),
                TotalAmount = g.Sum(x => x.Amount),
                WeightedAmount = g.Sum(x => Math.Round(x.Amount * x.Probability / 100m, 2))
            })
            .OrderByDescending(o => o.TotalAmount)
            .ToList();

        return new PipelineReportViewModel
        {
            TotalPipeline = totalPipeline,
            TotalWeightedPipeline = totalWeighted,
            TotalOpenDeals = openDeals.Count,
            StageBreakdowns = stageBreakdowns,
            OwnerBreakdowns = ownerGroups
        };
    }

    public async Task<byte[]> ExportPipelineReportCsvAsync(string currentUserId, IList<string> roles)
    {
        var model = await GetPipelineReportAsync(currentUserId, roles);
        var sb = new StringBuilder();
        sb.AppendLine("Pipeline Stage,Deal Count,Total Value,Weighted Value,Share (%)");
        foreach (var s in model.StageBreakdowns)
        {
            sb.AppendLine($"{EscapeCsv(s.Stage.ToString())},{s.DealCount},{s.TotalAmount:F2},{s.WeightedAmount:F2},{s.SharePercentage}");
        }
        sb.AppendLine();
        sb.AppendLine("Executive,Open Deals,Total Value,Weighted Value");
        foreach (var o in model.OwnerBreakdowns)
        {
            sb.AppendLine($"{EscapeCsv(o.UserName)},{o.DealCount},{o.TotalAmount:F2},{o.WeightedAmount:F2}");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
    #endregion

    #region 6. Conversion Report
    public async Task<ConversionReportViewModel> GetConversionReportAsync(string currentUserId, IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var query = _context.Leads
            .AsNoTracking();

        if (!isPrivileged)
        {
            query = query.Where(l => l.AssignedToUserId == currentUserId);
        }

        var leads = await query.ToListAsync();
        var totalLeads = leads.Count;
        var convertedLeads = leads.Count(l => l.Status == LeadStatus.Converted);
        var lostLeads = leads.Count(l => l.Status == LeadStatus.Lost);
        var openLeads = leads.Count(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost);

        // Sum won/converted opportunity revenue from converted leads
        var convertedLeadIds = leads.Where(l => l.Status == LeadStatus.Converted).Select(l => l.Id).ToHashSet();
        var allOpps = await _context.Opportunities
            .AsNoTracking()
            .Where(o => o.LeadId.HasValue)
            .Select(o => new { LeadId = o.LeadId!.Value, o.Amount })
            .ToListAsync();
        var convertedRevenue = allOpps.Where(o => convertedLeadIds.Contains(o.LeadId)).Sum(o => o.Amount);

        // Breakdown by Lead Source
        var sourceBreakdowns = leads
            .GroupBy(l => l.Source)
            .Select(g => new SourceConversionSummary
            {
                Source = g.Key,
                TotalLeads = g.Count(),
                ConvertedLeads = g.Count(x => x.Status == LeadStatus.Converted),
                LostLeads = g.Count(x => x.Status == LeadStatus.Lost)
            })
            .OrderByDescending(s => s.TotalLeads)
            .ToList();

        return new ConversionReportViewModel
        {
            TotalLeads = totalLeads,
            ConvertedLeads = convertedLeads,
            OpenLeads = openLeads,
            LostLeads = lostLeads,
            TotalConvertedRevenue = convertedRevenue,
            SourceBreakdowns = sourceBreakdowns
        };
    }

    public async Task<byte[]> ExportConversionReportCsvAsync(string currentUserId, IList<string> roles)
    {
        var model = await GetConversionReportAsync(currentUserId, roles);
        var sb = new StringBuilder();
        sb.AppendLine("Lead Source,Total Leads,Converted Leads,Lost Leads,Conversion Rate (%)");
        foreach (var s in model.SourceBreakdowns)
        {
            sb.AppendLine($"{EscapeCsv(s.Source)},{s.TotalLeads},{s.ConvertedLeads},{s.LostLeads},{s.ConversionRate}");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
    #endregion

    #region 7. User Activity Report
    public async Task<UserActivityReportViewModel> GetUserActivityReportAsync(
        DateTime? startDate,
        DateTime? endDate,
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var usersQuery = _userManager.Users.AsNoTracking();
        if (!isPrivileged)
        {
            usersQuery = usersQuery.Where(u => u.Id == currentUserId);
        }

        var users = await usersQuery.ToListAsync();

        var actQuery = _context.Activities.AsNoTracking();
        var fuQuery = _context.FollowUps.AsNoTracking();
        var oppQuery = _context.Opportunities.AsNoTracking().Where(o => o.Stage == OpportunityStage.Won);

        if (startDate.HasValue)
        {
            actQuery = actQuery.Where(a => a.ActivityDate >= startDate.Value.Date);
            fuQuery = fuQuery.Where(f => f.ScheduledDate >= startDate.Value.Date);
            oppQuery = oppQuery.Where(o => o.ExpectedCloseDate >= startDate.Value.Date);
        }
        if (endDate.HasValue)
        {
            actQuery = actQuery.Where(a => a.ActivityDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));
            fuQuery = fuQuery.Where(f => f.ScheduledDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));
            oppQuery = oppQuery.Where(o => o.ExpectedCloseDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        var allActivities = await actQuery.ToListAsync();
        var allFollowUps = await fuQuery.ToListAsync();
        var allWonDeals = await oppQuery.ToListAsync();

        var summaries = new List<UserActivitySummaryItem>();

        foreach (var user in users)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            var userActs = allActivities.Where(a => a.UserId == user.Id).ToList();
            var userFus = allFollowUps.Where(f => f.AssignedToUserId == user.Id && f.Status == FollowUpStatus.Completed).ToList();
            var userWon = allWonDeals.Where(o => o.AssignedToUserId == user.Id).ToList();

            summaries.Add(new UserActivitySummaryItem
            {
                UserId = user.Id,
                UserName = user.FullName,
                Role = userRoles.FirstOrDefault() ?? "SalesExecutive",
                TotalActivities = userActs.Count,
                CallsCount = userActs.Count(a => a.Type == ActivityType.Call),
                MeetingsCount = userActs.Count(a => a.Type == ActivityType.Meeting),
                EmailsCount = userActs.Count(a => a.Type == ActivityType.Email),
                TasksCount = userActs.Count(a => a.Type == ActivityType.Task),
                CompletedFollowUps = userFus.Count,
                WonDealsCount = userWon.Count,
                WonDealsValue = userWon.Sum(d => d.Amount)
            });
        }

        return new UserActivityReportViewModel
        {
            StartDate = startDate,
            EndDate = endDate,
            Summaries = summaries.OrderByDescending(s => s.TotalActivities).ToList()
        };
    }

    public async Task<byte[]> ExportUserActivityReportCsvAsync(DateTime? startDate, DateTime? endDate, string currentUserId, IList<string> roles)
    {
        var model = await GetUserActivityReportAsync(startDate, endDate, currentUserId, roles);
        var sb = new StringBuilder();
        sb.AppendLine("User,Role,Total Activities,Calls,Meetings,Emails,Tasks,Completed Follow-Ups,Won Deals,Won Revenue");
        foreach (var u in model.Summaries)
        {
            sb.AppendLine($"{EscapeCsv(u.UserName)},{EscapeCsv(u.Role)},{u.TotalActivities},{u.CallsCount},{u.MeetingsCount},{u.EmailsCount},{u.TasksCount},{u.CompletedFollowUps},{u.WonDealsCount},{u.WonDealsValue:F2}");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
    #endregion

    #region 8. Audit Report
    public async Task<AuditReportViewModel> GetAuditReportAsync(
        AuditReportViewModel filter,
        string currentUserId,
        IList<string> roles)
    {
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking();

        if (!isPrivileged)
        {
            query = query.Where(a => a.UserId == currentUserId);
        }
        else if (!string.IsNullOrEmpty(filter.AssignedToUserId))
        {
            query = query.Where(a => a.UserId == filter.AssignedToUserId);
        }

        if (!string.IsNullOrEmpty(filter.ActionFilter))
        {
            query = query.Where(a => a.Action == filter.ActionFilter);
        }

        if (!string.IsNullOrEmpty(filter.EntityNameFilter))
        {
            query = query.Where(a => a.EntityName == filter.EntityNameFilter);
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= filter.StartDate.Value.Date);
        }
        if (filter.EndDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= filter.EndDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Action.ToLower().Contains(term) ||
                a.EntityName.ToLower().Contains(term) ||
                (a.Details != null && a.Details.ToLower().Contains(term)));
        }

        filter.TotalItems = await query.CountAsync();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 15 : filter.PageSize;

        filter.Items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditReportItem
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = a.User != null ? a.User.FullName : "System",
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                Details = a.Details,
                IpAddress = a.IpAddress,
                Timestamp = a.Timestamp
            })
            .ToListAsync();

        filter.CanFilterUsers = isPrivileged;
        if (isPrivileged)
        {
            filter.AvailableUsers = await GetUserSelectListAsync(filter.AssignedToUserId);
        }

        return filter;
    }

    public async Task<byte[]> ExportAuditReportCsvAsync(AuditReportViewModel filter, string currentUserId, IList<string> roles)
    {
        filter.Page = 1;
        filter.PageSize = 10000;
        var report = await GetAuditReportAsync(filter, currentUserId, roles);

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,User,Action,Entity,Record ID,IP Address,Details");
        foreach (var a in report.Items)
        {
            sb.AppendLine($"{EscapeCsv(a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))},{EscapeCsv(a.UserName)},{EscapeCsv(a.Action)},{EscapeCsv(a.EntityName)},{EscapeCsv(a.EntityId ?? "")},{EscapeCsv(a.IpAddress ?? "")},{EscapeCsv(a.Details ?? "")}");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
    #endregion

    private async Task<List<SelectListItem>> GetUserSelectListAsync(string? selectedId)
    {
        var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
        return users.Select(u => new SelectListItem
        {
            Value = u.Id,
            Text = $"{u.FullName} ({u.Email})",
            Selected = u.Id == selectedId
        }).ToList();
    }
}
