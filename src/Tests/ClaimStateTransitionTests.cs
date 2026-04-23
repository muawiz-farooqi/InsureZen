using FluentAssertions;
using InsureZen.Data;
using InsureZen.Models.DTOs;
using InsureZen.Models.Entities;
using InsureZen.Models.Enums;
using InsureZen.Repositories;
using InsureZen.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsureZen.Tests;

/// <summary>
/// Unit + integration tests for claim state transition logic.
/// Uses EF Core In-Memory provider so no real database is required.
/// </summary>
public class ClaimStateTransitionTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ClaimsService _service;

    public ClaimStateTransitionTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // isolated per test
            .Options;

        _db = new AppDbContext(options);
        var repo = new ClaimsRepository(_db);
        _service = new ClaimsService(repo, NullLogger<ClaimsService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ClaimDto> IngestClaim(string company = "Test Insurance") =>
        await _service.IngestAsync(new ClaimCreateRequest
        {
            InsuranceCompany = company,
            ExternalClaimReference = $"CLM-{Guid.NewGuid():N}",
            StandardizedData = new StandardizedData
            {
                PatientName = "Jane Doe",
                PolicyNumber = "POL-001",
                ClaimAmount = 1500f
            }
        });

    // ── Ingestion ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ingest_ShouldCreate_ClaimWithStatusNew()
    {
        var dto = await IngestClaim();

        dto.Should().NotBeNull();
        dto.ClaimId.Should().NotBeEmpty();
        dto.Status.Should().Be("NEW");
        dto.AssignedTo.Should().BeNull();
    }

    [Fact]
    public async Task Ingest_ShouldStore_InsuranceCompany()
    {
        var dto = await IngestClaim("Al Sagr National Insurance");
        dto.InsuranceCompany.Should().Be("Al Sagr National Insurance");
    }

    // ── Maker Assign ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MakerAssign_OnNewClaim_ShouldTransitionTo_MakerAssigned()
    {
        var claim = await IngestClaim();

        var (dto, error, code) = await _service.MakerAssignAsync(
            claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);

        code.Should().Be(200);
        dto!.Status.Should().Be("MAKER_ASSIGNED");
        dto.AssignedTo.Should().Be("emp-001");
        dto.MakerId.Should().Be("emp-001");
        dto.AssignedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MakerAssign_OnAlreadyAssignedClaim_ShouldReturn_409()
    {
        var claim = await IngestClaim();
        await _service.MakerAssignAsync(
            claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);

        // Second Maker tries to grab the same claim
        var (dto, error, code) = await _service.MakerAssignAsync(
            claim.ClaimId, new MakerAssignRequest { MakerId = "emp-002" }, null);

        code.Should().Be(409);
        dto.Should().BeNull();
        error.Should().Contain("MAKER_ASSIGNED");
    }

    [Fact]
    public async Task MakerAssign_OnNonExistentClaim_ShouldReturn_404()
    {
        var (dto, error, code) = await _service.MakerAssignAsync(
            Guid.NewGuid(), new MakerAssignRequest { MakerId = "emp-001" }, null);

        code.Should().Be(404);
        dto.Should().BeNull();
    }

    // ── Maker Review ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MakerReview_AfterAssign_ShouldTransitionTo_MakerReviewed()
    {
        var claim = await IngestClaim();
        await _service.MakerAssignAsync(
            claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);

        var (dto, error, code) = await _service.MakerReviewAsync(
            claim.ClaimId,
            new MakerReviewRequest { Recommendation = "APPROVE", MakerFeedback = "Looks good" },
            null);

        code.Should().Be(200);
        dto!.Status.Should().Be("MAKER_REVIEWED");
        dto.MakerRecommendation.Should().Be("APPROVE");
        dto.MakerFeedback.Should().Be("Looks good");
        dto.MakerReviewedAt.Should().NotBeNull();
        dto.AssignedTo.Should().BeNull(); // released after review
    }

    [Fact]
    public async Task MakerReview_WithReject_ShouldStillTransitionTo_MakerReviewed()
    {
        var claim = await IngestClaim();
        await _service.MakerAssignAsync(
            claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);

        var (dto, _, code) = await _service.MakerReviewAsync(
            claim.ClaimId,
            new MakerReviewRequest { Recommendation = "REJECT", MakerFeedback = "Missing docs" },
            null);

        code.Should().Be(200);
        dto!.Status.Should().Be("MAKER_REVIEWED"); // rejection is advisory, not a blocker
        dto.MakerRecommendation.Should().Be("REJECT");
    }

    [Fact]
    public async Task MakerReview_WithoutPriorAssignment_ShouldReturn_400()
    {
        var claim = await IngestClaim(); // status = NEW, not MAKER_ASSIGNED

        var (dto, error, code) = await _service.MakerReviewAsync(
            claim.ClaimId,
            new MakerReviewRequest { Recommendation = "APPROVE" },
            null);

        code.Should().Be(400);
        dto.Should().BeNull();
        error.Should().Contain("MAKER_ASSIGNED");
    }

    [Fact]
    public async Task MakerReview_WithInvalidRecommendation_ShouldReturn_400()
    {
        var claim = await IngestClaim();
        await _service.MakerAssignAsync(
            claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);

        var (dto, error, code) = await _service.MakerReviewAsync(
            claim.ClaimId,
            new MakerReviewRequest { Recommendation = "MAYBE" }, // invalid
            null);

        code.Should().Be(400);
        dto.Should().BeNull();
        error.Should().Contain("APPROVE or REJECT");
    }

    // ── Checker Assign ────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckerAssign_AfterMakerReview_ShouldTransitionTo_CheckerAssigned()
    {
        var claim = await IngestClaim();
        await _service.MakerAssignAsync(claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);
        await _service.MakerReviewAsync(claim.ClaimId, new MakerReviewRequest { Recommendation = "APPROVE" }, null);

        var (dto, error, code) = await _service.CheckerAssignAsync(
            claim.ClaimId, new CheckerAssignRequest { CheckerId = "emp-042" }, null);

        code.Should().Be(200);
        dto!.Status.Should().Be("CHECKER_ASSIGNED");
        dto.CheckerId.Should().Be("emp-042");
        dto.AssignedTo.Should().Be("emp-042");
    }

    [Fact]
    public async Task CheckerAssign_OnNewClaim_ShouldReturn_409()
    {
        var claim = await IngestClaim(); // status = NEW, not MAKER_REVIEWED

        var (dto, error, code) = await _service.CheckerAssignAsync(
            claim.ClaimId, new CheckerAssignRequest { CheckerId = "emp-042" }, null);

        code.Should().Be(409);
        dto.Should().BeNull();
    }

    [Fact]
    public async Task CheckerAssign_OnAlreadyAssignedClaim_ShouldReturn_409()
    {
        var claim = await IngestClaim();
        await _service.MakerAssignAsync(claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);
        await _service.MakerReviewAsync(claim.ClaimId, new MakerReviewRequest { Recommendation = "APPROVE" }, null);
        await _service.CheckerAssignAsync(claim.ClaimId, new CheckerAssignRequest { CheckerId = "emp-042" }, null);

        // Second Checker tries to grab same claim
        var (dto, error, code) = await _service.CheckerAssignAsync(
            claim.ClaimId, new CheckerAssignRequest { CheckerId = "emp-099" }, null);

        code.Should().Be(409);
        dto.Should().BeNull();
    }

    // ── Checker Review ────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckerReview_AfterCheckerAssign_ShouldTransitionTo_Forwarded()
    {
        var claim = await IngestClaim("Daman Insurance");
        await _service.MakerAssignAsync(claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);
        await _service.MakerReviewAsync(claim.ClaimId, new MakerReviewRequest { Recommendation = "APPROVE" }, null);
        await _service.CheckerAssignAsync(claim.ClaimId, new CheckerAssignRequest { CheckerId = "emp-042" }, null);

        var (dto, error, code) = await _service.CheckerReviewAsync(
            claim.ClaimId,
            new CheckerReviewRequest { Decision = "APPROVE", CheckerFeedback = "All verified" },
            null);

        code.Should().Be(200);
        dto!.Status.Should().Be("FORWARDED");
        dto.CheckerDecision.Should().Be("APPROVE");
        dto.CheckerFeedback.Should().Be("All verified");
        dto.CheckerReviewedAt.Should().NotBeNull();
        dto.ForwardedAt.Should().NotBeNull();
        dto.ForwardedTo.Should().Be("Daman Insurance");
    }

    [Fact]
    public async Task CheckerReview_WithReject_ShouldAlsoForward()
    {
        var claim = await IngestClaim();
        await _service.MakerAssignAsync(claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);
        await _service.MakerReviewAsync(claim.ClaimId, new MakerReviewRequest { Recommendation = "REJECT" }, null);
        await _service.CheckerAssignAsync(claim.ClaimId, new CheckerAssignRequest { CheckerId = "emp-042" }, null);

        var (dto, _, code) = await _service.CheckerReviewAsync(
            claim.ClaimId,
            new CheckerReviewRequest { Decision = "REJECT" },
            null);

        code.Should().Be(200);
        dto!.Status.Should().Be("FORWARDED");
        dto.CheckerDecision.Should().Be("REJECT");
    }

    [Fact]
    public async Task CheckerReview_WithoutPriorAssignment_ShouldReturn_400()
    {
        var claim = await IngestClaim();
        await _service.MakerAssignAsync(claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);
        await _service.MakerReviewAsync(claim.ClaimId, new MakerReviewRequest { Recommendation = "APPROVE" }, null);
        // Skipped checker-assign — status = MAKER_REVIEWED

        var (dto, error, code) = await _service.CheckerReviewAsync(
            claim.ClaimId,
            new CheckerReviewRequest { Decision = "APPROVE" },
            null);

        code.Should().Be(400);
        dto.Should().BeNull();
        error.Should().Contain("CHECKER_ASSIGNED");
    }

    [Fact]
    public async Task CheckerReview_WithInvalidDecision_ShouldReturn_400()
    {
        var claim = await IngestClaim();
        await _service.MakerAssignAsync(claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);
        await _service.MakerReviewAsync(claim.ClaimId, new MakerReviewRequest { Recommendation = "APPROVE" }, null);
        await _service.CheckerAssignAsync(claim.ClaimId, new CheckerAssignRequest { CheckerId = "emp-042" }, null);

        var (dto, error, code) = await _service.CheckerReviewAsync(
            claim.ClaimId,
            new CheckerReviewRequest { Decision = "PENDING" }, // invalid
            null);

        code.Should().Be(400);
        error.Should().Contain("APPROVE or REJECT");
    }

    // ── Full Happy Path ───────────────────────────────────────────────────────

    [Fact]
    public async Task FullWorkflow_HappyPath_ShouldEndInForwardedState()
    {
        // 1. Ingest
        var claim = await IngestClaim("Emirates Insurance");
        claim.Status.Should().Be("NEW");

        // 2. Maker assigns
        var (assigned, _, _) = await _service.MakerAssignAsync(
            claim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);
        assigned!.Status.Should().Be("MAKER_ASSIGNED");

        // 3. Maker reviews
        var (reviewed, _, _) = await _service.MakerReviewAsync(
            claim.ClaimId,
            new MakerReviewRequest { Recommendation = "APPROVE", MakerFeedback = "All clear" },
            null);
        reviewed!.Status.Should().Be("MAKER_REVIEWED");

        // 4. Checker assigns
        var (checkerAssigned, _, _) = await _service.CheckerAssignAsync(
            claim.ClaimId, new CheckerAssignRequest { CheckerId = "emp-042" }, null);
        checkerAssigned!.Status.Should().Be("CHECKER_ASSIGNED");

        // 5. Checker reviews & auto-forwards
        var (forwarded, _, _) = await _service.CheckerReviewAsync(
            claim.ClaimId,
            new CheckerReviewRequest { Decision = "APPROVE", CheckerFeedback = "Confirmed" },
            null);
        forwarded!.Status.Should().Be("FORWARDED");
        forwarded.ForwardedTo.Should().Be("Emirates Insurance");
        forwarded.ForwardedAt.Should().NotBeNull();
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingClaim_ShouldReturnDto()
    {
        var ingested = await IngestClaim();
        var dto = await _service.GetByIdAsync(ingested.ClaimId);
        dto.Should().NotBeNull();
        dto!.ClaimId.Should().Be(ingested.ClaimId);
    }

    [Fact]
    public async Task GetById_NonExistentClaim_ShouldReturnNull()
    {
        var dto = await _service.GetByIdAsync(Guid.NewGuid());
        dto.Should().BeNull();
    }

    // ── Available Queues ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableForMaker_ShouldOnlyReturn_NewClaims()
    {
        await IngestClaim("Company A");
        await IngestClaim("Company B");

        var thirdClaim = await IngestClaim("Company C");
        await _service.MakerAssignAsync(
            thirdClaim.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);

        var available = await _service.GetAvailableForMakerAsync();

        available.Should().HaveCount(2);
        available.Should().AllSatisfy(c => c.Status.Should().Be("NEW"));
    }

    [Fact]
    public async Task GetAvailableForChecker_ShouldOnlyReturn_MakerReviewedClaims()
    {
        var c1 = await IngestClaim();
        await _service.MakerAssignAsync(c1.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);
        await _service.MakerReviewAsync(c1.ClaimId, new MakerReviewRequest { Recommendation = "APPROVE" }, null);

        await IngestClaim(); // still NEW — should NOT appear

        var available = await _service.GetAvailableForCheckerAsync();

        available.Should().HaveCount(1);
        available.Single().Status.Should().Be("MAKER_REVIEWED");
    }

    // ── Paginated History ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetPaginated_ShouldRespect_PageSize()
    {
        for (int i = 0; i < 5; i++) await IngestClaim();

        var result = await _service.GetPaginatedAsync(new ClaimQueryParams { Page = 1, Size = 3 });

        result.Items.Should().HaveCount(3);
        result.Total.Should().Be(5);
        result.Page.Should().Be(1);
        result.Size.Should().Be(3);
    }

    [Fact]
    public async Task GetPaginated_FilterByStatus_ShouldReturn_MatchingClaimsOnly()
    {
        var c1 = await IngestClaim();
        await IngestClaim();
        await _service.MakerAssignAsync(c1.ClaimId, new MakerAssignRequest { MakerId = "emp-001" }, null);

        var result = await _service.GetPaginatedAsync(
            new ClaimQueryParams { Page = 1, Size = 20, Status = "MAKER_ASSIGNED" });

        result.Items.Should().HaveCount(1);
        result.Items.Single().Status.Should().Be("MAKER_ASSIGNED");
    }

    [Fact]
    public async Task GetPaginated_FilterByInsuranceCompany_ShouldReturn_MatchingOnly()
    {
        await IngestClaim("Daman Insurance");
        await IngestClaim("Daman Insurance");
        await IngestClaim("Al Sagr National Insurance");

        var result = await _service.GetPaginatedAsync(
            new ClaimQueryParams { Page = 1, Size = 20, InsuranceCompany = "Daman" });

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
    }
}