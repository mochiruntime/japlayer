using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Japlayer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaLocationDurationSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "durationSeconds",
                table: "MediaLocations",
                type: "INT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "durationSeconds",
                table: "MediaLocations");
        }
    }
}
