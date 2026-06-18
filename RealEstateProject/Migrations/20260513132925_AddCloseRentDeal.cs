using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateProject.Migrations
{
    /// <inheritdoc />
    public partial class AddCloseRentDeal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRentClosed",
                table: "Enquiries",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRentClosed",
                table: "Enquiries");
        }
    }
}
