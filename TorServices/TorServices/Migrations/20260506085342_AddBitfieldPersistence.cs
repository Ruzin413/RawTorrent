using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TorServices.Migrations
{
    /// <inheritdoc />
    public partial class AddBitfieldPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Bitfield",
                table: "TorrentProgresses",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bitfield",
                table: "TorrentProgresses");
        }
    }
}
