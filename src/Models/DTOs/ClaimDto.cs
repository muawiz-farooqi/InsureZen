using InsureZen.Models.Entities;
using InsureZen.Models.Enums;

namespace InsureZen.Models.DTOs;

public class ClaimDto
{
    public Guid ClaimId { get; set; }
    public string? ExternalClaimReference { get; set; }
    public string InsuranceCompany { get; set; } = string.Empty;
    public DateTime SubmissionDate { get; set; }
    public StandardizedData StandardizedData { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public DateTime? AssignedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;

    // Maker
    public string? MakerId { get; set; }
    public string? MakerFeedback { get; set; }
    public string? MakerRecommendation { get; set; }
    public DateTime? MakerReviewedAt { get; set; }

    // Checker
    public string? CheckerId { get; set; }
    public string? CheckerDecision { get; set; }
    public string? CheckerFeedback { get; set; }
    public DateTime? CheckerReviewedAt { get; set; }

    // Forwarding
    public DateTime? ForwardedAt { get; set; }
    public string? ForwardedTo { get; set; }

    // System
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PaginatedClaimsResponse
{
    public List<ClaimDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
}