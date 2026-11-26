using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CqrsApiExample.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "Accounts",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "Accounts",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Accounts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Accounts",
                table: "Users");
        }
    }
}
