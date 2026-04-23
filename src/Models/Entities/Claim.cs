using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InsureZen.Models.Enums;

namespace InsureZen.Models.Entities;

public class Claim
{
    [Key]
    public Guid ClaimId { get; set; } = Guid.NewGuid();

    public string? ExternalClaimReference { get; set; }

    [Required]
    public string InsuranceCompany { get; set; } = string.Empty;

    public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;

    // Stored as JSON column
    [Column(TypeName = "jsonb")]
    public StandardizedData StandardizedData { get; set; } = new();

    public ClaimStatus Status { get; set; } = ClaimStatus.NEW;

    public string? AssignedTo { get; set; }
    public DateTime? AssignedAt { get; set; }

    // Maker audit fields
    public string? MakerId { get; set; }
    public string? MakerFeedback { get; set; }
    public Recommendation? MakerRecommendation { get; set; }
    public DateTime? MakerReviewedAt { get; set; }

    // Checker audit fields
    public string? CheckerId { get; set; }
    public Recommendation? CheckerDecision { get; set; }
    public string? CheckerFeedback { get; set; }
    public DateTime? CheckerReviewedAt { get; set; }

    // Forwarding
    public DateTime? ForwardedAt { get; set; }
    public string? ForwardedTo { get; set; }

    // System timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // EF Core optimistic concurrency token (maps to PostgreSQL xmin)
    [Timestamp]
    public uint RowVersion { get; set; }
}

public class StandardizedData
{
    public string? PatientName { get; set; }
    public DateOnly? PatientDateOfBirth { get; set; }
    public string? PolicyNumber { get; set; }
    public float? ClaimAmount { get; set; }
    public List<string> DiagnosisCodes { get; set; } = [];
    public List<string> ProcedureCodes { get; set; } = [];
    public DateOnly? ServiceDate { get; set; }
    public string? ProviderName { get; set; }
    public float? TotalBilledAmount { get; set; }
    public float? InsuranceCoveredAmount { get; set; }
    public float? PatientResponsibility { get; set; }
}