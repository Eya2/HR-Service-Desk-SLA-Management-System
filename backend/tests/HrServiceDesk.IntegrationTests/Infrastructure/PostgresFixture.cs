using Testcontainers.PostgreSql;

namespace HrServiceDesk.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL container and one API host (migrated and seeded) shared by every test in the
/// "postgres" collection. Tests that change state create their own users so they stay independent.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("hrdesk")
        .WithUsername("hrdesk")
        .WithPassword("hrdesk")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ApiFactory Api { get; private set; } = null!;

    /// <summary>Connection string for a separate database on the same server.</summary>
    public string ConnectionStringFor(string database) =>
        new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString) { Database = database }.ConnectionString;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Api = new ApiFactory(ConnectionString);
        _ = Api.Server; // starts the host: migrations and demo seed run once here
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
