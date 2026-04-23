using InsureZen.Data;
using InsureZen.Models.DTOs;
using InsureZen.Models.Entities;
using InsureZen.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsureZen.Repositories;

public interface IClaimsRepository
{
    Task<Claim> CreateAsync(Claim claim);
    Task<Claim?> GetByIdAsync(Guid id);
    Task<List<Claim>> GetAvailableForMakerAsync();
    Task<List<Claim>> GetAvailableForCheckerAsync();
    Task<PaginatedResult<Claim>> GetPaginatedAsync(ClaimQueryParams p);
    Task<Claim> UpdateAsync(Claim claim);
}

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int Total { get; set; }
}

public class ClaimQueryParams
{
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 20;
    public string? Status { get; set; }
    public string? InsuranceCompany { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public class ClaimsRepository(AppDbContext db) : IClaimsRepository
{
    public async Task<Claim> CreateAsync(Claim claim)
    {
        db.Claims.Add(claim);
        await db.SaveChangesAsync();
        return claim;
    }

    public Task<Claim?> GetByIdAsync(Guid id) =>
        db.Claims.FirstOrDefaultAsync(c => c.ClaimId == id);

    public Task<List<Claim>> GetAvailableForMakerAsync() =>
        db.Claims.Where(c => c.Status == ClaimStatus.NEW).ToListAsync();

    public Task<List<Claim>> GetAvailableForCheckerAsync() =>
        db.Claims.Where(c => c.Status == ClaimStatus.MAKER_REVIEWED).ToListAsync();

    public async Task<PaginatedResult<Claim>> GetPaginatedAsync(ClaimQueryParams p)
    {
        var q = db.Claims.AsQueryable();

        if (!string.IsNullOrEmpty(p.Status) && Enum.TryParse<ClaimStatus>(p.Status, true, out var st))
            q = q.Where(c => c.Status == st);

        if (!string.IsNullOrEmpty(p.InsuranceCompany))
            q = q.Where(c => c.InsuranceCompany.Contains(p.InsuranceCompany));

        if (p.StartDate.HasValue)
            q = q.Where(c => DateOnly.FromDateTime(c.SubmissionDate) >= p.StartDate.Value);

        if (p.EndDate.HasValue)
            q = q.Where(c => DateOnly.FromDateTime(c.SubmissionDate) <= p.EndDate.Value);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(c => c.CreatedAt)
                           .Skip((p.Page - 1) * p.Size)
                           .Take(p.Size)
                           .ToListAsync();

        return new PaginatedResult<Claim> { Items = items, Total = total };
    }

    public async Task<Claim> UpdateAsync(Claim claim)
    {
        claim.UpdatedAt = DateTime.UtcNow;
        db.Claims.Update(claim);
        await db.SaveChangesAsync();
        return claim;
    }
}