using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TorServices.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MagnetUri",
                table: "Torrents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TorrentPath",
                table: "Torrents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MagnetUri",
                table: "Torrents");

            migrationBuilder.DropColumn(
                name: "TorrentPath",
                table: "Torrents");
        }
    }
}
