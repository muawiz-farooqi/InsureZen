using InsureZen.Models.DTOs;
using InsureZen.Models.Entities;
using InsureZen.Models.Enums;
using InsureZen.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InsureZen.Services;

public interface IClaimsService
{
    Task<ClaimDto> IngestAsync(ClaimCreateRequest request);
    Task<ClaimDto?> GetByIdAsync(Guid id);
    Task<List<ClaimDto>> GetAvailableForMakerAsync();
    Task<List<ClaimDto>> GetAvailableForCheckerAsync();
    Task<PaginatedClaimsResponse> GetPaginatedAsync(ClaimQueryParams p);
    Task<(ClaimDto? Dto, string? Error, int StatusCode)> MakerAssignAsync(Guid id, MakerAssignRequest req, string? rowVersion);
    Task<(ClaimDto? Dto, string? Error, int StatusCode)> MakerReviewAsync(Guid id, MakerReviewRequest req, string? rowVersion);
    Task<(ClaimDto? Dto, string? Error, int StatusCode)> CheckerAssignAsync(Guid id, CheckerAssignRequest req, string? rowVersion);
    Task<(ClaimDto? Dto, string? Error, int StatusCode)> CheckerReviewAsync(Guid id, CheckerReviewRequest req, string? rowVersion);
}

public class ClaimsService(IClaimsRepository repo, ILogger<ClaimsService> logger) : IClaimsService
{
    public async Task<ClaimDto> IngestAsync(ClaimCreateRequest request)
    {
        var claim = new Claim
        {
            ExternalClaimReference = request.ExternalClaimReference,
            InsuranceCompany = request.InsuranceCompany,
            SubmissionDate = request.SubmissionDate?.ToUniversalTime() ?? DateTime.UtcNow,
            StandardizedData = request.StandardizedData,
            Status = ClaimStatus.NEW
        };
        await repo.CreateAsync(claim);
        logger.LogInformation("Claim {Id} ingested", claim.ClaimId);
        return Map(claim);
    }

    public async Task<ClaimDto?> GetByIdAsync(Guid id)
    {
        var claim = await repo.GetByIdAsync(id);
        return claim is null ? null : Map(claim);
    }

    public async Task<List<ClaimDto>> GetAvailableForMakerAsync() =>
        (await repo.GetAvailableForMakerAsync()).Select(Map).ToList();

    public async Task<List<ClaimDto>> GetAvailableForCheckerAsync() =>
        (await repo.GetAvailableForCheckerAsync()).Select(Map).ToList();

    public async Task<PaginatedClaimsResponse> GetPaginatedAsync(ClaimQueryParams p)
    {
        var result = await repo.GetPaginatedAsync(p);
        return new PaginatedClaimsResponse
        {
            Items = result.Items.Select(Map).ToList(),
            Total = result.Total,
            Page = p.Page,
            Size = p.Size
        };
    }

    public async Task<(ClaimDto?, string?, int)> MakerAssignAsync(Guid id, MakerAssignRequest req, string? rowVersion)
    {
        var claim = await repo.GetByIdAsync(id);
        if (claim is null) return (null, "Claim not found", 404);
        if (claim.Status != ClaimStatus.NEW)
            return (null, $"Claim must be in status NEW to assign a Maker. Current: {claim.Status}", 409);

        ApplyConcurrencyToken(claim, rowVersion);

        claim.AssignedTo = req.MakerId;
        claim.AssignedAt = DateTime.UtcNow;
        claim.MakerId = req.MakerId;
        claim.Status = ClaimStatus.MAKER_ASSIGNED;

        try
        {
            await repo.UpdateAsync(claim);
            logger.LogInformation("Claim {Id} assigned to Maker {Maker}", id, req.MakerId);
            return (Map(claim), null, 200);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (null, "Concurrency conflict: claim was modified by another request.", 409);
        }
    }

    public async Task<(ClaimDto?, string?, int)> MakerReviewAsync(Guid id, MakerReviewRequest req, string? rowVersion)
    {
        var claim = await repo.GetByIdAsync(id);
        if (claim is null) return (null, "Claim not found", 404);
        if (claim.Status != ClaimStatus.MAKER_ASSIGNED)
            return (null, $"Claim must be in status MAKER_ASSIGNED to submit a review. Current: {claim.Status}", 400);

        if (!Enum.TryParse<Recommendation>(req.Recommendation, true, out var rec))
            return (null, "Invalid recommendation value. Must be APPROVE or REJECT.", 400);

        ApplyConcurrencyToken(claim, rowVersion);

        claim.MakerRecommendation = rec;
        claim.MakerFeedback = req.MakerFeedback;
        claim.MakerReviewedAt = DateTime.UtcNow;
        claim.Status = ClaimStatus.MAKER_REVIEWED;
        claim.AssignedTo = null;

        try
        {
            await repo.UpdateAsync(claim);
            logger.LogInformation("Maker review submitted for Claim {Id}", id);
            return (Map(claim), null, 200);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (null, "Concurrency conflict.", 409);
        }
    }

    public async Task<(ClaimDto?, string?, int)> CheckerAssignAsync(Guid id, CheckerAssignRequest req, string? rowVersion)
    {
        var claim = await repo.GetByIdAsync(id);
        if (claim is null) return (null, "Claim not found", 404);
        if (claim.Status != ClaimStatus.MAKER_REVIEWED)
            return (null, $"Claim must be in status MAKER_REVIEWED to assign a Checker. Current: {claim.Status}", 409);

        ApplyConcurrencyToken(claim, rowVersion);

        claim.AssignedTo = req.CheckerId;
        claim.AssignedAt = DateTime.UtcNow;
        claim.CheckerId = req.CheckerId;
        claim.Status = ClaimStatus.CHECKER_ASSIGNED;

        try
        {
            await repo.UpdateAsync(claim);
            logger.LogInformation("Claim {Id} assigned to Checker {Checker}", id, req.CheckerId);
            return (Map(claim), null, 200);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (null, "Concurrency conflict: claim was modified by another request.", 409);
        }
    }

    public async Task<(ClaimDto?, string?, int)> CheckerReviewAsync(Guid id, CheckerReviewRequest req, string? rowVersion)
    {
        var claim = await repo.GetByIdAsync(id);
        if (claim is null) return (null, "Claim not found", 404);
        if (claim.Status != ClaimStatus.CHECKER_ASSIGNED)
            return (null, $"Claim must be in status CHECKER_ASSIGNED to submit a decision. Current: {claim.Status}", 400);

        if (!Enum.TryParse<Recommendation>(req.Decision, true, out var decision))
            return (null, "Invalid decision value. Must be APPROVE or REJECT.", 400);

        ApplyConcurrencyToken(claim, rowVersion);

        claim.CheckerDecision = decision;
        claim.CheckerFeedback = req.CheckerFeedback;
        claim.CheckerReviewedAt = DateTime.UtcNow;
        claim.Status = ClaimStatus.COMPLETED;
        claim.AssignedTo = null;

        // Auto-forward
        claim.Status = ClaimStatus.FORWARDED;
        claim.ForwardedAt = DateTime.UtcNow;
        claim.ForwardedTo = claim.InsuranceCompany;

        try
        {
            await repo.UpdateAsync(claim);
            logger.LogInformation("Claim {Id} completed and forwarded to {Company}", id, claim.InsuranceCompany);
            return (Map(claim), null, 200);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (null, "Concurrency conflict.", 409);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void ApplyConcurrencyToken(Claim claim, string? rowVersion)
    {
        // In Phase 2 (PostgreSQL), the xmin column is managed by the DB engine.
        // The If-Match header is used for client-side validation only; EF Core
        // handles the actual optimistic concurrency via the [Timestamp] token.
        // No manual assignment needed here — EF Core tracks it automatically.
    }

    private static ClaimDto Map(Claim c) => new()
    {
        ClaimId = c.ClaimId,
        ExternalClaimReference = c.ExternalClaimReference,
        InsuranceCompany = c.InsuranceCompany,
        SubmissionDate = c.SubmissionDate,
        StandardizedData = c.StandardizedData,
        Status = c.Status.ToString(),
        AssignedTo = c.AssignedTo,
        AssignedAt = c.AssignedAt,
        RowVersion = Convert.ToBase64String(BitConverter.GetBytes(c.RowVersion)),
        MakerId = c.MakerId,
        MakerFeedback = c.MakerFeedback,
        MakerRecommendation = c.MakerRecommendation?.ToString(),
        MakerReviewedAt = c.MakerReviewedAt,
        CheckerId = c.CheckerId,
        CheckerDecision = c.CheckerDecision?.ToString(),
        CheckerFeedback = c.CheckerFeedback,
        CheckerReviewedAt = c.CheckerReviewedAt,
        ForwardedAt = c.ForwardedAt,
        ForwardedTo = c.ForwardedTo,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}