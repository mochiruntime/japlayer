using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Japlayer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "UserTags",
                type: "TEXT",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.CreateTable(
                name: "LibraryPresets",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    isCustomName = table.Column<bool>(type: "INTEGER", nullable: false),
                    searchText = table.Column<string>(type: "TEXT", nullable: false),
                    sortField = table.Column<string>(type: "TEXT", nullable: false),
                    isSortDescending = table.Column<bool>(type: "INTEGER", nullable: false),
                    ratingMin = table.Column<double>(type: "REAL", nullable: false),
                    ratingMax = table.Column<double>(type: "REAL", nullable: false),
                    runtimeMin = table.Column<double>(type: "REAL", nullable: false),
                    runtimeMax = table.Column<double>(type: "REAL", nullable: true),
                    createdAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryPresets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "LibraryPresetGenres",
                columns: table => new
                {
                    libraryPresetId = table.Column<int>(type: "INTEGER", nullable: false),
                    genreId = table.Column<int>(type: "INTEGER", nullable: false),
                    isIncluded = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryPresetGenres", x => new { x.libraryPresetId, x.genreId });
                    table.ForeignKey(
                        name: "FK_LibraryPresetGenres_LibraryPresets_libraryPresetId",
                        column: x => x.libraryPresetId,
                        principalTable: "LibraryPresets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryPresetGenres_MediaGenres_genreId",
                        column: x => x.genreId,
                        principalTable: "MediaGenres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LibraryPresetPeople",
                columns: table => new
                {
                    libraryPresetId = table.Column<int>(type: "INTEGER", nullable: false),
                    personId = table.Column<int>(type: "INTEGER", nullable: false),
                    isIncluded = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryPresetPeople", x => new { x.libraryPresetId, x.personId });
                    table.ForeignKey(
                        name: "FK_LibraryPresetPeople_LibraryPresets_libraryPresetId",
                        column: x => x.libraryPresetId,
                        principalTable: "LibraryPresets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryPresetPeople_MediaPeople_personId",
                        column: x => x.personId,
                        principalTable: "MediaPeople",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LibraryPresetSeries",
                columns: table => new
                {
                    libraryPresetId = table.Column<int>(type: "INTEGER", nullable: false),
                    seriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    isIncluded = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryPresetSeries", x => new { x.libraryPresetId, x.seriesId });
                    table.ForeignKey(
                        name: "FK_LibraryPresetSeries_LibraryPresets_libraryPresetId",
                        column: x => x.libraryPresetId,
                        principalTable: "LibraryPresets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryPresetSeries_MediaSeries_seriesId",
                        column: x => x.seriesId,
                        principalTable: "MediaSeries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LibraryPresetStudios",
                columns: table => new
                {
                    libraryPresetId = table.Column<int>(type: "INTEGER", nullable: false),
                    studioId = table.Column<int>(type: "INTEGER", nullable: false),
                    isIncluded = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryPresetStudios", x => new { x.libraryPresetId, x.studioId });
                    table.ForeignKey(
                        name: "FK_LibraryPresetStudios_LibraryPresets_libraryPresetId",
                        column: x => x.libraryPresetId,
                        principalTable: "LibraryPresets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryPresetStudios_MediaStudios_studioId",
                        column: x => x.studioId,
                        principalTable: "MediaStudios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LibraryPresetTags",
                columns: table => new
                {
                    libraryPresetId = table.Column<int>(type: "INTEGER", nullable: false),
                    userTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    isIncluded = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryPresetTags", x => new { x.libraryPresetId, x.userTagId });
                    table.ForeignKey(
                        name: "FK_LibraryPresetTags_LibraryPresets_libraryPresetId",
                        column: x => x.libraryPresetId,
                        principalTable: "LibraryPresets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryPresetTags_UserTags_userTagId",
                        column: x => x.userTagId,
                        principalTable: "UserTags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Merge any pre-existing case-duplicate UserTags onto the lowest id before creating the unique NOCASE index.
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO UserTagsRelationship (mediaId, userTagId)
                SELECT r.mediaId, canonical.canonicalId
                FROM UserTagsRelationship r
                INNER JOIN UserTags t ON t.id = r.userTagId
                INNER JOIN (
                    SELECT LOWER(name) AS normalizedName, MIN(id) AS canonicalId
                    FROM UserTags
                    GROUP BY LOWER(name)
                ) canonical ON LOWER(t.name) = canonical.normalizedName
                WHERE r.userTagId <> canonical.canonicalId;

                DELETE FROM UserTagsRelationship
                WHERE userTagId NOT IN (
                    SELECT MIN(id) FROM UserTags GROUP BY LOWER(name)
                );

                DELETE FROM UserTags
                WHERE id NOT IN (
                    SELECT MIN(id) FROM UserTags GROUP BY LOWER(name)
                );
                """);

            migrationBuilder.CreateIndex(
                name: "UserTags_name",
                table: "UserTags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LibraryPresetGenres_genreId",
                table: "LibraryPresetGenres",
                column: "genreId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryPresetPeople_personId",
                table: "LibraryPresetPeople",
                column: "personId");

            migrationBuilder.CreateIndex(
                name: "LibraryPresets_name",
                table: "LibraryPresets",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LibraryPresetSeries_seriesId",
                table: "LibraryPresetSeries",
                column: "seriesId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryPresetStudios_studioId",
                table: "LibraryPresetStudios",
                column: "studioId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryPresetTags_userTagId",
                table: "LibraryPresetTags",
                column: "userTagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LibraryPresetGenres");

            migrationBuilder.DropTable(
                name: "LibraryPresetPeople");

            migrationBuilder.DropTable(
                name: "LibraryPresetSeries");

            migrationBuilder.DropTable(
                name: "LibraryPresetStudios");

            migrationBuilder.DropTable(
                name: "LibraryPresetTags");

            migrationBuilder.DropTable(
                name: "LibraryPresets");

            migrationBuilder.DropIndex(
                name: "UserTags_name",
                table: "UserTags");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "UserTags",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldCollation: "NOCASE");
        }
    }
}
