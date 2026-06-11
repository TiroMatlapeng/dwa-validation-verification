using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class AddLetterIssuanceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data repair (must run BEFORE the unique index): before this invariant
            // existed, the un-guarded issue flow could create more than one ORIGINAL
            // (ReissuedFromId IS NULL) letter for the same (FileMasterId, LetterTypeId).
            // Those rows would make CREATE UNIQUE INDEX fail. These are STATUTORY LEGAL
            // letters, so we never delete them: instead we keep the earliest original per
            // (file, letter type) and re-point the surplus rows as reissues of the kept
            // original (ReissuedFromId = keeper). That removes them from the filtered
            // index population while preserving every row + the audit trail.
            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT LetterIssuanceId, FileMasterId, LetterTypeId,
           ROW_NUMBER() OVER (
               PARTITION BY FileMasterId, LetterTypeId
               ORDER BY GeneratedDate, IssuedDate, LetterIssuanceId
           ) AS rn
    FROM LetterIssuances
    WHERE ReissuedFromId IS NULL
),
keepers AS (
    SELECT FileMasterId, LetterTypeId, LetterIssuanceId AS KeepId
    FROM ranked
    WHERE rn = 1
)
UPDATE li
SET ReissuedFromId = k.KeepId
FROM LetterIssuances li
INNER JOIN ranked  r ON li.LetterIssuanceId = r.LetterIssuanceId AND r.rn > 1
INNER JOIN keepers k ON k.FileMasterId = r.FileMasterId AND k.LetterTypeId = r.LetterTypeId;
");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuance_FileMaster_LetterType_Original",
                table: "LetterIssuances",
                columns: new[] { "FileMasterId", "LetterTypeId" },
                unique: true,
                filter: "[ReissuedFromId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LetterIssuance_FileMaster_LetterType_Original",
                table: "LetterIssuances");
        }
    }
}
