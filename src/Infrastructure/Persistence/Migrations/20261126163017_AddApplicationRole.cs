using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CqrsApiExample.Infrastructure.Persistence.Migrations
{
    /// <summary>
    ///     Creates the role the API runs as (<see cref="DatabaseRoles.Application"/>): data access to
    ///     its own schemas, and nothing else. Logins are server-level and differ per environment, so
    ///     they are not created here; each environment adds its login's user to this role.
    /// </summary>
    /// <inheritdoc />
    public partial class AddApplicationRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                                  IF DATABASE_PRINCIPAL_ID(N'{DatabaseRoles.Application}') IS NULL
                                      CREATE ROLE [{DatabaseRoles.Application}];
                                  """);

            // Rows only: no ALTER, CREATE, REFERENCES or EXECUTE, and nothing in dbo.
            migrationBuilder.Sql($"GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[{Schemas.Accounts}] TO [{DatabaseRoles.Application}];");
            migrationBuilder.Sql($"GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[{Schemas.Messaging}] TO [{DatabaseRoles.Application}];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Members must leave the role before it can be dropped.
            migrationBuilder.Sql($"""
                                  DECLARE @sql nvarchar(max) = N'';
                                  SELECT @sql += N'ALTER ROLE [{DatabaseRoles.Application}] DROP MEMBER ' + QUOTENAME(member.name) + N';'
                                  FROM sys.database_role_members AS membership
                                  JOIN sys.database_principals AS member ON member.principal_id = membership.member_principal_id
                                  WHERE membership.role_principal_id = DATABASE_PRINCIPAL_ID(N'{DatabaseRoles.Application}');
                                  EXEC (@sql);
                                  DROP ROLE IF EXISTS [{DatabaseRoles.Application}];
                                  """);
        }
    }
}
