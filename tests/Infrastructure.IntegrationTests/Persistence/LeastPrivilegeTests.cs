using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Infrastructure.Data;
using CqrsApiExample.Infrastructure.Persistence.Ado;
using CqrsApiExample.Tests.Shared;
using FluentAssertions;
using Microsoft.Data.SqlClient;

namespace CqrsApiExample.Infrastructure.IntegrationTests.Persistence;

/// <summary>
///     The API's database login (a member of cqrs_api_app_role, created by the migrations) can
///     read and write rows in its own schemas, and nothing else.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class LeastPrivilegeTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private readonly TestDatabase _database = new(sqlServer);
    private string _applicationConnectionString = null!;

    public async Task InitializeAsync()
    {
        await _database.CreateAsync();

        // A login of its own per test: logins are server-wide and the container is shared.
        _applicationConnectionString = new SqlConnectionStringBuilder(_database.ConnectionString)
        {
            UserID = $"app_{Guid.NewGuid():N}",
            Password = "Test_App_Passw0rd!"
        }.ConnectionString;

        await DevelopmentDatabaseLogin.EnsureAsync(_database.ConnectionString, _applicationConnectionString, default);
    }

    public Task DisposeAsync() => _database.DeleteAsync();

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task Application_Login_Can_Write_And_Read_Users_And_Outbox_Rows()
    {
        // Arrange: the ADO.NET provider on the restricted login
        var connections = new SqlConnectionFactory(_applicationConnectionString);
        var changes = new AdoChangeSet();
        var repository = new AdoUserRepository(connections, changes);
        var unitOfWork = new AdoUnitOfWork(connections, changes, _database.Clock, _database.CurrentUser);
        var user = User.Create("John", "Doe", "john@doe.com", "hashed-password").Value;

        // Act: Accounts (the user) and Messaging (its outbox row) in one transaction
        repository.Add(user);
        await unitOfWork.SaveChangesAsync(default);
        var page = await new AdoUserQueries(connections).GetPageAsync(1, 10, default);

        // Assert
        page.Items.Should().ContainSingle(saved => saved.Id == user.Id);
        (await ScalarAsync("SELECT COUNT(*) FROM [Messaging].[OutboxMessages]")).Should().Be(1);
    }

    [Theory]
    [Trait("Category", "Persistence")]
    [InlineData("CREATE TABLE [Accounts].[Intruder] ([Id] int)")]
    [InlineData("DROP TABLE [Accounts].[Users]")]
    [InlineData("ALTER TABLE [Accounts].[Users] ADD [Extra] int NULL")]
    [InlineData("SELECT COUNT(*) FROM [dbo].[__EFMigrationsHistory]")]
    [InlineData("CREATE LOGIN [intruder] WITH PASSWORD = 'Intruder_Passw0rd!'")]
    [InlineData("GRANT CONTROL ON SCHEMA::[Accounts] TO [public]")]
    public async Task Application_Login_Cannot_Change_The_Schema_Or_Leave_Its_Schemas(string sql)
    {
        // Act
        var act = () => ScalarAsync(sql);

        // Assert: SQL Server refuses with a permission error, whatever the statement
        await act.Should().ThrowAsync<SqlException>();
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task EnsureAsync_Is_Idempotent_And_Skips_The_Migrations_Login()
    {
        // Act: a second run, and a run where the API uses the migrations login itself
        await DevelopmentDatabaseLogin.EnsureAsync(_database.ConnectionString, _applicationConnectionString, default);
        await DevelopmentDatabaseLogin.EnsureAsync(_database.ConnectionString, _database.ConnectionString, default);

        // Assert: still exactly one member in the role
        await using var connection = new SqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.database_role_members WHERE role_principal_id = DATABASE_PRINCIPAL_ID(N'cqrs_api_app_role')",
            connection);
        ((int)(await command.ExecuteScalarAsync())!).Should().Be(1);
    }

    private async Task<int> ScalarAsync(string sql)
    {
        await using var connection = new SqlConnection(_applicationConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return result is int count ? count : 0;
    }
}
