using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Japlayer.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Media",
                columns: table => new
                {
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Media", x => x.mediaId);
                });

            migrationBuilder.CreateTable(
                name: "MediaGenres",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaGenres", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MediaPeople",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaPeople", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MediaSeries",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaSeries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MediaStudios",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaStudios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "UserTags",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MediaHighlights",
                columns: table => new
                {
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    scene = table.Column<int>(type: "INT", nullable: false),
                    timestamp = table.Column<int>(type: "INT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaHighlights", x => new { x.mediaId, x.scene, x.timestamp });
                    table.ForeignKey(
                        name: "FK_MediaHighlights_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaImages",
                columns: table => new
                {
                    filepath = table.Column<string>(type: "TEXT", nullable: false),
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    url = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaImages", x => x.filepath);
                    table.ForeignKey(
                        name: "FK_MediaImages_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaLocations",
                columns: table => new
                {
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    scene = table.Column<int>(type: "INT", nullable: false, defaultValue: 1),
                    hostname = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    createdAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaLocations", x => new { x.mediaId, x.scene, x.hostname, x.path });
                    table.ForeignKey(
                        name: "FK_MediaLocations_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaThumbnails",
                columns: table => new
                {
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    scene = table.Column<int>(type: "INT", nullable: false),
                    timestamp = table.Column<int>(type: "INT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    originalHostname = table.Column<string>(type: "TEXT", nullable: false),
                    originalPath = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaThumbnails", x => new { x.mediaId, x.scene, x.timestamp });
                    table.ForeignKey(
                        name: "FK_MediaThumbnails_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMediaData",
                columns: table => new
                {
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    rating = table.Column<int>(type: "INT", nullable: true),
                    totalPlayTimeSeconds = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    lastPlayedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMediaData", x => x.mediaId);
                    table.ForeignKey(
                        name: "FK_UserMediaData_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GenresRelationship",
                columns: table => new
                {
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    genre = table.Column<int>(type: "INT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenresRelationship", x => new { x.mediaId, x.genre });
                    table.ForeignKey(
                        name: "FK_GenresRelationship_MediaGenres_genre",
                        column: x => x.genre,
                        principalTable: "MediaGenres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GenresRelationship_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CastRelationship",
                columns: table => new
                {
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    person = table.Column<int>(type: "INT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CastRelationship", x => new { x.mediaId, x.person });
                    table.ForeignKey(
                        name: "FK_CastRelationship_MediaPeople_person",
                        column: x => x.person,
                        principalTable: "MediaPeople",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CastRelationship_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffRelationship",
                columns: table => new
                {
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    person = table.Column<int>(type: "INT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffRelationship", x => new { x.mediaId, x.person });
                    table.ForeignKey(
                        name: "FK_StaffRelationship_MediaPeople_person",
                        column: x => x.person,
                        principalTable: "MediaPeople",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffRelationship_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SeriesRelationship",
                columns: table => new
                {
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    series = table.Column<int>(type: "INT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesRelationship", x => new { x.mediaId, x.series });
                    table.ForeignKey(
                        name: "FK_SeriesRelationship_MediaSeries_series",
                        column: x => x.series,
                        principalTable: "MediaSeries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeriesRelationship_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudiosRelationship",
                columns: table => new
                {
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    studio = table.Column<int>(type: "INT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudiosRelationship", x => new { x.mediaId, x.studio });
                    table.ForeignKey(
                        name: "FK_StudiosRelationship_MediaStudios_studio",
                        column: x => x.studio,
                        principalTable: "MediaStudios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudiosRelationship_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserTagsRelationship",
                columns: table => new
                {
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    userTagId = table.Column<int>(type: "INT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTagsRelationship", x => new { x.mediaId, x.userTagId });
                    table.ForeignKey(
                        name: "FK_UserTagsRelationship_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserTagsRelationship_UserTags_userTagId",
                        column: x => x.userTagId,
                        principalTable: "UserTags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaMetadata",
                columns: table => new
                {
                    metadataUrl = table.Column<string>(type: "TEXT", nullable: false),
                    mediaId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    contentId = table.Column<string>(type: "TEXT", nullable: true),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    cover = table.Column<string>(type: "TEXT", nullable: true),
                    thumbnail = table.Column<string>(type: "TEXT", nullable: true),
                    releaseDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    runtimeMinutes = table.Column<int>(type: "INT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaMetadata", x => x.metadataUrl);
                    table.ForeignKey(
                        name: "FK_MediaMetadata_MediaImages_cover",
                        column: x => x.cover,
                        principalTable: "MediaImages",
                        principalColumn: "filepath",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediaMetadata_MediaImages_thumbnail",
                        column: x => x.thumbnail,
                        principalTable: "MediaImages",
                        principalColumn: "filepath",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediaMetadata_Media_mediaId",
                        column: x => x.mediaId,
                        principalTable: "Media",
                        principalColumn: "mediaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CastRelationship_person",
                table: "CastRelationship",
                column: "person");

            migrationBuilder.CreateIndex(
                name: "IX_GenresRelationship_genre",
                table: "GenresRelationship",
                column: "genre");

            migrationBuilder.CreateIndex(
                name: "MediaGenres_name",
                table: "MediaGenres",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaImages_mediaId",
                table: "MediaImages",
                column: "mediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadata_cover",
                table: "MediaMetadata",
                column: "cover");

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadata_mediaId",
                table: "MediaMetadata",
                column: "mediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadata_thumbnail",
                table: "MediaMetadata",
                column: "thumbnail");

            migrationBuilder.CreateIndex(
                name: "MediaPeople_name",
                table: "MediaPeople",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "MediaSeries_name",
                table: "MediaSeries",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "MediaStudios_name",
                table: "MediaStudios",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeriesRelationship_series",
                table: "SeriesRelationship",
                column: "series");

            migrationBuilder.CreateIndex(
                name: "IX_StaffRelationship_person",
                table: "StaffRelationship",
                column: "person");

            migrationBuilder.CreateIndex(
                name: "IX_StudiosRelationship_studio",
                table: "StudiosRelationship",
                column: "studio");

            migrationBuilder.CreateIndex(
                name: "IX_UserTagsRelationship_userTagId",
                table: "UserTagsRelationship",
                column: "userTagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CastRelationship");

            migrationBuilder.DropTable(
                name: "GenresRelationship");

            migrationBuilder.DropTable(
                name: "MediaHighlights");

            migrationBuilder.DropTable(
                name: "MediaLocations");

            migrationBuilder.DropTable(
                name: "MediaMetadata");

            migrationBuilder.DropTable(
                name: "MediaThumbnails");

            migrationBuilder.DropTable(
                name: "SeriesRelationship");

            migrationBuilder.DropTable(
                name: "StaffRelationship");

            migrationBuilder.DropTable(
                name: "StudiosRelationship");

            migrationBuilder.DropTable(
                name: "UserMediaData");

            migrationBuilder.DropTable(
                name: "UserTagsRelationship");

            migrationBuilder.DropTable(
                name: "MediaGenres");

            migrationBuilder.DropTable(
                name: "MediaImages");

            migrationBuilder.DropTable(
                name: "MediaSeries");

            migrationBuilder.DropTable(
                name: "MediaPeople");

            migrationBuilder.DropTable(
                name: "MediaStudios");

            migrationBuilder.DropTable(
                name: "UserTags");

            migrationBuilder.DropTable(
                name: "Media");
        }
    }
}
