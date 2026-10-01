using Testcontainers.PostgreSql;

namespace HrServiceDesk.IntegrationTests.Infrastructure;

/// <summary>One PostgreSQL container shared by every test in the "postgres" collection.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("hrdesk")
        .WithUsername("hrdesk")
        .WithPassword("hrdesk")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Connection string for a separate database on the same server.</summary>
    public string ConnectionStringFor(string database) =>
        new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString) { Database = database }.ConnectionString;

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
