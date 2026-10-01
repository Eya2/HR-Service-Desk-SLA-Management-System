using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Infrastructure.Persistence;
using HrServiceDesk.Infrastructure.Persistence.Interceptors;
using HrServiceDesk.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.IntegrationTests.Persistence;

/// <summary>
/// Verifies the multi-tenant guarantees of <see cref="AppDbContext"/> on a real PostgreSQL database,
/// using a test-only tenant-owned entity (business entities arrive in later phases).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TenantIsolationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private readonly string _connectionString = postgres.ConnectionStringFor("tenant_isolation_tests");

    public async Task InitializeAsync()
    {
        await using var db = CreateContext(tenant: null);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        db.Notes.AddRange(
            new TestNote { TenantId = TenantA, Text = "a1" },
            new TestNote { TenantId = TenantA, Text = "a2" },
            new TestNote { TenantId = TenantB, Text = "b1" });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Queries_only_return_rows_of_the_current_tenant()
    {
        await using var db = CreateContext(TenantA);

        var texts = await db.Notes.Select(n => n.Text).OrderBy(t => t).ToListAsync();

        texts.Should().Equal("a1", "a2");
    }

    [Fact]
    public async Task Queries_return_nothing_when_no_tenant_is_resolved()
    {
        await using var db = CreateContext(tenant: null);

        (await db.Notes.CountAsync()).Should().Be(0);
        (await db.Notes.IgnoreQueryFilters().CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Same_context_type_filters_per_instance_tenant()
    {
        await using var dbA = CreateContext(TenantA);
        await using var dbB = CreateContext(TenantB);

        (await dbA.Notes.CountAsync()).Should().Be(2);
        (await dbB.Notes.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Insert_stamps_the_current_tenant()
    {
        await using var db = CreateContext(TenantB);
        var note = new TestNote { Text = "b2" };

        db.Notes.Add(note);
        await db.SaveChangesAsync();

        note.TenantId.Should().Be(TenantB);
        note.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Insert_for_another_tenant_is_blocked()
    {
        await using var db = CreateContext(TenantA);
        db.Notes.Add(new TestNote { TenantId = TenantB, Text = "sneaky" });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cross-tenant*");
    }

    [Fact]
    public async Task Insert_without_any_tenant_is_blocked()
    {
        await using var db = CreateContext(tenant: null);
        db.Notes.Add(new TestNote { Text = "orphan" });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no tenant*");
    }

    [Fact]
    public async Task Changing_tenant_of_an_existing_row_is_blocked()
    {
        await using var db = CreateContext(TenantA);
        var note = await db.Notes.FirstAsync();

        note.TenantId = TenantB;
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot be changed*");
    }

    private TestDbContext CreateContext(Guid? tenant)
    {
        var tenantContext = new FixedTenantContext(tenant);
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new TenantStampingInterceptor(tenantContext), new AuditableInterceptor(TimeProvider.System))
            .Options;
        return new TestDbContext(options, tenantContext);
    }

    private sealed record FixedTenantContext(Guid? TenantId) : ITenantContext;

    public sealed class TestNote : Entity, ITenantOwned, IAuditable
    {
        public Guid TenantId { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenantContext)
        : AppDbContext(options, tenantContext)
    {
        public DbSet<TestNote> Notes => Set<TestNote>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Register before the base class applies tenant filters by convention.
            modelBuilder.Entity<TestNote>();
            base.OnModelCreating(modelBuilder);
        }
    }
}
