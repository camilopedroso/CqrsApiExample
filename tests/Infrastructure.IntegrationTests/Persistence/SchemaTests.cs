using CqrsApiExample.Infrastructure.Persistence;
using CqrsApiExample.Tests.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CqrsApiExample.Infrastructure.IntegrationTests.Persistence;

/// <summary>
///     Checks the database the migrations actually create: every table lives in
///     a named schema (<see cref="Schemas" />), never in <c>dbo</c>.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class SchemaTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private readonly TestDatabase _database = new(sqlServer);

    public Task InitializeAsync() => _database.CreateAsync();

    public Task DisposeAsync() => _database.DeleteAsync();

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task Migrations_Should_Create_Tables_In_Their_Schemas()
    {
        // Arrange
        await using var context = _database.CreateContext();

        // Act
        var tables = await context.Database
                                  .SqlQuery<string>($"""
                                                     SELECT s.name + '.' + t.name AS Value
                                                     FROM sys.tables t
                                                     JOIN sys.schemas s ON s.schema_id = t.schema_id
                                                     WHERE t.name <> '__EFMigrationsHistory'
                                                     """)
                                  .ToListAsync();

        // Assert
        tables.Should().BeEquivalentTo(
            $"{Schemas.Accounts}.Users",
            $"{Schemas.Messaging}.OutboxMessages",
            $"{Schemas.Messaging}.OutboxMessageConsumers");
    }
}
