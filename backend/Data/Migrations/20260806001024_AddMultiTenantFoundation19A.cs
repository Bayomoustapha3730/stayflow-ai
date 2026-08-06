using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenantFoundation19A : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrandingLogoUrl",
                table: "Companies",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandingPrimaryColor",
                table: "Companies",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedSlug",
                table: "Companies",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OnboardingState",
                table: "Companies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Companies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Companies",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Companies",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "OrganizationMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
WITH slug_base AS (
    SELECT
        c.""Id"",
        NULLIF(TRIM(BOTH '-' FROM REGEXP_REPLACE(LOWER(c.""Name""), '[^a-z0-9]+', '-', 'g')), '') AS base_slug
    FROM ""Companies"" c
),
slug_dedup AS (
    SELECT
        s.""Id"",
        COALESCE(s.base_slug, 'organization') AS base_slug,
        ROW_NUMBER() OVER (PARTITION BY COALESCE(s.base_slug, 'organization') ORDER BY s.""Id"") AS rn
    FROM slug_base s
)
UPDATE ""Companies"" c
SET
    ""Slug"" = CASE WHEN d.rn = 1 THEN d.base_slug ELSE d.base_slug || '-' || d.rn::text END,
    ""NormalizedSlug"" = UPPER(CASE WHEN d.rn = 1 THEN d.base_slug ELSE d.base_slug || '-' || d.rn::text END),
    ""Status"" = CASE WHEN c.""IsActive"" THEN 'Active' ELSE 'Inactive' END
FROM slug_dedup d
WHERE c.""Id"" = d.""Id"";

INSERT INTO ""OrganizationMembers"" (""Id"", ""CompanyId"", ""UserId"", ""Role"", ""Status"", ""JoinedAt"", ""InvitedByUserId"", ""CreatedAt"", ""UpdatedAt"")
SELECT
    (
        SUBSTRING(MD5(u.""CompanyId""::text || ':' || u.""Id""::text), 1, 8) || '-' ||
        SUBSTRING(MD5(u.""CompanyId""::text || ':' || u.""Id""::text), 9, 4) || '-' ||
        SUBSTRING(MD5(u.""CompanyId""::text || ':' || u.""Id""::text), 13, 4) || '-' ||
        SUBSTRING(MD5(u.""CompanyId""::text || ':' || u.""Id""::text), 17, 4) || '-' ||
        SUBSTRING(MD5(u.""CompanyId""::text || ':' || u.""Id""::text), 21, 12)
    )::uuid,
    u.""CompanyId"",
    u.""Id"",
    CASE
        WHEN LOWER(COALESCE(u.""Role"", '')) IN ('owner') THEN 'Owner'
        WHEN LOWER(COALESCE(u.""Role"", '')) IN ('admin', 'administrator') THEN 'Administrator'
        WHEN LOWER(COALESCE(u.""Role"", '')) IN ('manager') THEN 'Manager'
        WHEN LOWER(COALESCE(u.""Role"", '')) IN ('host') THEN 'Host'
        WHEN LOWER(COALESCE(u.""Role"", '')) IN ('support') THEN 'Support'
        ELSE 'ReadOnly'
    END,
    'Active',
    COALESCE(u.""CreatedAt"", NOW()),
    NULL,
    COALESCE(u.""CreatedAt"", NOW()),
    NOW()
FROM ""Users"" u
WHERE u.""IsActive"" = TRUE
  AND NOT EXISTS (
      SELECT 1
      FROM ""OrganizationMembers"" m
      WHERE m.""CompanyId"" = u.""CompanyId""
        AND m.""UserId"" = u.""Id""
        AND m.""Status"" = 'Active');

UPDATE ""Companies"" c
SET ""OwnerUserId"" = COALESCE(
    (
        SELECT m.""UserId""
        FROM ""OrganizationMembers"" m
        WHERE m.""CompanyId"" = c.""Id""
          AND m.""Status"" = 'Active'
          AND m.""Role"" = 'Owner'
        ORDER BY m.""JoinedAt"" ASC
        LIMIT 1
    ),
    (
        SELECT m.""UserId""
        FROM ""OrganizationMembers"" m
        WHERE m.""CompanyId"" = c.""Id""
          AND m.""Status"" = 'Active'
          AND m.""Role"" = 'Administrator'
        ORDER BY m.""JoinedAt"" ASC
        LIMIT 1
    ),
    (
        SELECT u.""Id""
        FROM ""Users"" u
        WHERE u.""CompanyId"" = c.""Id""
          AND u.""IsActive"" = TRUE
        ORDER BY u.""CreatedAt"" ASC
        LIMIT 1
    )
)
WHERE c.""OwnerUserId"" IS NULL;

UPDATE ""OrganizationMembers"" m
SET ""Role"" = 'Owner',
    ""UpdatedAt"" = NOW()
FROM ""Companies"" c
WHERE c.""OwnerUserId"" IS NOT NULL
  AND m.""CompanyId"" = c.""Id""
  AND m.""UserId"" = c.""OwnerUserId""
  AND m.""Status"" = 'Active';

UPDATE ""Companies""
SET ""Status"" = CASE WHEN ""IsActive"" THEN 'Active' ELSE 'Inactive' END
WHERE ""Status"" IS NULL OR ""Status"" = '';
");

            migrationBuilder.UpdateData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "BrandingLogoUrl", "BrandingPrimaryColor", "NormalizedSlug", "OnboardingState", "OwnerUserId", "Slug", "Status" },
                values: new object[] { null, null, "STAYFLOW-DEMO-HOSTS", null, null, "stayflow-demo-hosts", "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_NormalizedSlug",
                table: "Companies",
                column: "NormalizedSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_OwnerUserId",
                table: "Companies",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Slug",
                table: "Companies",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Status",
                table: "Companies",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_CompanyId",
                table: "OrganizationMembers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_CompanyId_UserId",
                table: "OrganizationMembers",
                columns: new[] { "CompanyId", "UserId" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_InvitedByUserId",
                table: "OrganizationMembers",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_Role",
                table: "OrganizationMembers",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_Status",
                table: "OrganizationMembers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_UserId",
                table: "OrganizationMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Users_OwnerUserId",
                table: "Companies",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Users_OwnerUserId",
                table: "Companies");

            migrationBuilder.DropTable(
                name: "OrganizationMembers");

            migrationBuilder.DropIndex(
                name: "IX_Companies_NormalizedSlug",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Companies_OwnerUserId",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Companies_Slug",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Companies_Status",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "BrandingLogoUrl",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "BrandingPrimaryColor",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "NormalizedSlug",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "OnboardingState",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Companies");
        }
    }
}
