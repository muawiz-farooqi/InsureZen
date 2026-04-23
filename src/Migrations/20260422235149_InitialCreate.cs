using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsureZen.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalClaimReference = table.Column<string>(type: "text", nullable: true),
                    InsuranceCompany = table.Column<string>(type: "text", nullable: false),
                    SubmissionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MakerId = table.Column<string>(type: "text", nullable: true),
                    MakerFeedback = table.Column<string>(type: "text", nullable: true),
                    MakerRecommendation = table.Column<string>(type: "text", nullable: true),
                    MakerReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckerId = table.Column<string>(type: "text", nullable: true),
                    CheckerDecision = table.Column<string>(type: "text", nullable: true),
                    CheckerFeedback = table.Column<string>(type: "text", nullable: true),
                    CheckerReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ForwardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ForwardedTo = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    StandardizedData = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.ClaimId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_InsuranceCompany",
                table: "Claims",
                column: "InsuranceCompany");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Status",
                table: "Claims",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_SubmissionDate",
                table: "Claims",
                column: "SubmissionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Claims");
        }
    }
}
