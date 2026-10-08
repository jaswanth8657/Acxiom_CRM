namespace AcxiomCRM.Models;

public enum CustomerStatus
{
    Active = 1,
    Inactive = 2
}

public enum LeadStatus
{
    New = 1,
    Contacted = 2,
    Qualified = 3,
    Unqualified = 4,
    Converted = 5,
    Lost = 6
}

public static class LeadSources
{
    public const string Website = "Website";
    public const string Referral = "Referral";
    public const string ColdCall = "Cold Call";
    public const string TradeShow = "Trade Show";
    public const string EmailCampaign = "Email Campaign";
    public const string SocialMedia = "Social Media";
    public const string Partner = "Partner";
    public const string Inbound = "Inbound";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        Website,
        Referral,
        ColdCall,
        TradeShow,
        EmailCampaign,
        SocialMedia,
        Partner,
        Inbound,
        Other
    ];
}

public enum OpportunityStatus
{
    Active = 1,
    Inactive = 2
}

public enum OpportunityStage
{
    Prospecting = 1,
    Qualification = 2,
    Proposal = 3,
    Negotiation = 4,
    Won = 5,
    ClosedWon = 5,
    Lost = 6,
    ClosedLost = 6
}

public enum FollowUpStatus
{
    Planned = 1,
    Scheduled = 1,
    Completed = 2,
    Cancelled = 3,
    Missed = 4,
    Overdue = 4
}

public enum FollowUpType
{
    Call = 1,
    Meeting = 2,
    Email = 3,
    Task = 4,
    Other = 5
}

public enum FollowUpPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

public enum ActivityType
{
    Call = 1,
    Email = 2,
    Meeting = 3,
    Task = 4,
    Note = 5
}

public enum ActivityStatus
{
    Planned = 1,
    Completed = 2,
    Cancelled = 3
}
