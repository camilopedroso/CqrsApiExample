using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CqrsApiExample.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveTablesToSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Messaging");

            migrationBuilder.EnsureSchema(
                name: "Accounts");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "Users",
                newSchema: "Accounts");

            migrationBuilder.RenameTable(
                name: "OutboxMessages",
                newName: "OutboxMessages",
                newSchema: "Messaging");

            migrationBuilder.RenameTable(
                name: "OutboxMessageConsumers",
                newName: "OutboxMessageConsumers",
                newSchema: "Messaging");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Users",
                schema: "Accounts",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "OutboxMessages",
                schema: "Messaging",
                newName: "OutboxMessages");

            migrationBuilder.RenameTable(
                name: "OutboxMessageConsumers",
                schema: "Messaging",
                newName: "OutboxMessageConsumers");
        }
    }
}
