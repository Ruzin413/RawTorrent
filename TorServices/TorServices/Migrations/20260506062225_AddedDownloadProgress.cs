using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TorServices.Migrations
{
    /// <inheritdoc />
    public partial class AddedDownloadProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Progress",
                table: "Torrents");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Torrents");

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "Torrents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserClientId",
                table: "Torrents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TorrentProgresses",
                columns: table => new
                {
                    TorrentId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Progress = table.Column<double>(type: "double precision", nullable: false),
                    TotalPieces = table.Column<int>(type: "integer", nullable: false),
                    CompletedPieces = table.Column<int>(type: "integer", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TorrentProgresses", x => x.TorrentId);
                    table.ForeignKey(
                        name: "FK_TorrentProgresses_Torrents_TorrentId",
                        column: x => x.TorrentId,
                        principalTable: "Torrents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClients",
                columns: table => new
                {
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClients", x => x.ClientId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Torrents_UserClientId",
                table: "Torrents",
                column: "UserClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Torrents_UserClients_UserClientId",
                table: "Torrents",
                column: "UserClientId",
                principalTable: "UserClients",
                principalColumn: "ClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Torrents_UserClients_UserClientId",
                table: "Torrents");

            migrationBuilder.DropTable(
                name: "TorrentProgresses");

            migrationBuilder.DropTable(
                name: "UserClients");

            migrationBuilder.DropIndex(
                name: "IX_Torrents_UserClientId",
                table: "Torrents");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Torrents");

            migrationBuilder.DropColumn(
                name: "UserClientId",
                table: "Torrents");

            migrationBuilder.AddColumn<double>(
                name: "Progress",
                table: "Torrents",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Torrents",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
