using FluentValidation;
using FluentValidation.AspNetCore;
using InsureZen.Data;
using InsureZen.Repositories;
using InsureZen.Services;
using InsureZen.Validators;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var conn = builder.Configuration.GetConnectionString("DefaultConnection")
           ?? "Host=db;Database=insurezen;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(conn));

// ── DI ────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IClaimsRepository, ClaimsRepository>();
builder.Services.AddScoped<IClaimsService, ClaimsService>();

// ── Validation ────────────────────────────────────────────────────────────────
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<ClaimCreateRequestValidator>();

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "InsureZen Claims API", Version = "v1" });
});

// ── Problem Details ───────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();

var app = builder.Build();

// ── Migrations on startup ─────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ── Middleware ────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.MapControllers();
app.Run();