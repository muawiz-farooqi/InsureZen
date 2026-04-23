using InsureZen.Models.DTOs;
using InsureZen.Repositories;
using InsureZen.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsureZen.Controllers;

[ApiController]
[Route("api/claims")]
[Produces("application/json")]
public class ClaimsController(IClaimsService service) : ControllerBase
{
    // POST /api/claims — Ingest
    [HttpPost]
    [ProducesResponseType(typeof(ClaimDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Ingest([FromBody] ClaimCreateRequest request)
    {
        var dto = await service.IngestAsync(request);
        return CreatedAtAction(nameof(GetById), new { claimId = dto.ClaimId }, dto);
    }

    // GET /api/claims — Paginated history
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedClaimsResponse), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? insuranceCompany = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var result = await service.GetPaginatedAsync(new ClaimQueryParams
        {
            Page = page, Size = size, Status = status,
            InsuranceCompany = insuranceCompany,
            StartDate = startDate, EndDate = endDate
        });
        return Ok(result);
    }

    // GET /api/claims/history — Alias
    [HttpGet("history")]
    [ProducesResponseType(typeof(PaginatedClaimsResponse), 200)]
    public Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? insuranceCompany = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
        => GetAll(page, size, status, insuranceCompany, startDate, endDate);

    // GET /api/claims/{claimId}
    [HttpGet("{claimId:guid}")]
    [ProducesResponseType(typeof(ClaimDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid claimId)
    {
        var dto = await service.GetByIdAsync(claimId);
        return dto is null ? NotFound(Problem("Claim not found", statusCode: 404)) : Ok(dto);
    }

    // GET /api/claims/available/maker
    [HttpGet("available/maker")]
    [ProducesResponseType(typeof(List<ClaimDto>), 200)]
    public async Task<IActionResult> AvailableForMaker()
        => Ok(await service.GetAvailableForMakerAsync());

    // GET /api/claims/available/checker
    [HttpGet("available/checker")]
    [ProducesResponseType(typeof(List<ClaimDto>), 200)]
    public async Task<IActionResult> AvailableForChecker()
        => Ok(await service.GetAvailableForCheckerAsync());

    // PATCH /api/claims/{claimId}/maker-assign
    [HttpPatch("{claimId:guid}/maker-assign")]
    [ProducesResponseType(typeof(ClaimDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> MakerAssign(Guid claimId, [FromBody] MakerAssignRequest req)
    {
        var ifMatch = Request.Headers["If-Match"].FirstOrDefault();
        var (dto, error, code) = await service.MakerAssignAsync(claimId, req, ifMatch);
        return code switch
        {
            200 => Ok(dto),
            404 => NotFound(Problem(error, statusCode: 404)),
            _ => Conflict(Problem(error, statusCode: 409))
        };
    }

    // PATCH /api/claims/{claimId}/maker-review
    [HttpPatch("{claimId:guid}/maker-review")]
    [ProducesResponseType(typeof(ClaimDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> MakerReview(Guid claimId, [FromBody] MakerReviewRequest req)
    {
        var ifMatch = Request.Headers["If-Match"].FirstOrDefault();
        var (dto, error, code) = await service.MakerReviewAsync(claimId, req, ifMatch);
        return code switch
        {
            200 => Ok(dto),
            404 => NotFound(Problem(error, statusCode: 404)),
            400 => BadRequest(Problem(error, statusCode: 400)),
            _ => Conflict(Problem(error, statusCode: 409))
        };
    }

    // PATCH /api/claims/{claimId}/checker-assign
    [HttpPatch("{claimId:guid}/checker-assign")]
    [ProducesResponseType(typeof(ClaimDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CheckerAssign(Guid claimId, [FromBody] CheckerAssignRequest req)
    {
        var ifMatch = Request.Headers["If-Match"].FirstOrDefault();
        var (dto, error, code) = await service.CheckerAssignAsync(claimId, req, ifMatch);
        return code switch
        {
            200 => Ok(dto),
            404 => NotFound(Problem(error, statusCode: 404)),
            _ => Conflict(Problem(error, statusCode: 409))
        };
    }

    // PATCH /api/claims/{claimId}/checker-review
    [HttpPatch("{claimId:guid}/checker-review")]
    [ProducesResponseType(typeof(ClaimDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CheckerReview(Guid claimId, [FromBody] CheckerReviewRequest req)
    {
        var ifMatch = Request.Headers["If-Match"].FirstOrDefault();
        var (dto, error, code) = await service.CheckerReviewAsync(claimId, req, ifMatch);
        return code switch
        {
            200 => Ok(dto),
            404 => NotFound(Problem(error, statusCode: 404)),
            400 => BadRequest(Problem(error, statusCode: 400)),
            _ => Conflict(Problem(error, statusCode: 409))
        };
    }

    private ObjectResult Problem(string? detail, int statusCode) =>
        StatusCode(statusCode, new { title = GetTitle(statusCode), status = statusCode, detail });

    private static string GetTitle(int code) => code switch
    {
        400 => "Bad Request",
        404 => "Not Found",
        409 => "Conflict",
        _ => "Error"
    };
}