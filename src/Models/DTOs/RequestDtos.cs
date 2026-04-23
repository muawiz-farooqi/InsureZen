using InsureZen.Models.Entities;

namespace InsureZen.Models.DTOs;

public class ClaimCreateRequest
{
    public string? ExternalClaimReference { get; set; }
    public string InsuranceCompany { get; set; } = string.Empty;
    public DateTime? SubmissionDate { get; set; }
    public StandardizedData StandardizedData { get; set; } = new();
}

public class MakerAssignRequest
{
    public string MakerId { get; set; } = string.Empty;
}

public class MakerReviewRequest
{
    public string Recommendation { get; set; } = string.Empty;
    public string? MakerFeedback { get; set; }
}

public class CheckerAssignRequest
{
    public string CheckerId { get; set; } = string.Empty;
}

public class CheckerReviewRequest
{
    public string Decision { get; set; } = string.Empty;
    public string? CheckerFeedback { get; set; }
}