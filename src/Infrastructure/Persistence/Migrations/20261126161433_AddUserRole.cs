using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CqrsApiExample.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "Accounts",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                // Users that existed before roles are members; nobody becomes an administrator by migration.
                defaultValue: "Member");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                schema: "Accounts",
                table: "Users");
        }
    }
}
