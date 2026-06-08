using Xunit;

namespace dwa_ver_val.Tests.Integration;

/// <summary>
/// Serializes ALL SQL-Server-backed integration tests into a single xUnit collection so they
/// never run concurrently.
///
/// Why: each WebApplicationFactory-based test class boots its own host and calls
/// <c>db.Database.MigrateAsync()</c> against the shared dev database on startup. xUnit runs
/// distinct test classes in parallel by default, so two of these could migrate the same
/// database at the same instant — an intermittent concurrent-migration failure (the X-01
/// shared-dev-DB weakness). Tests in the SAME collection never run in parallel with each
/// other, which removes the contention. The dedicated-DB race tests (EnsureCreated on their
/// own databases) are folded in too, so no two SQL tests ever overlap at the server level.
///
/// The fast InMemory unit tests live in other (implicit) collections and keep running fully
/// in parallel — only the SQL-touching tests are serialised.
///
/// This is a marker collection (no shared fixture): classes keep their own
/// <c>IClassFixture&lt;...&gt;</c>; the collection only controls parallelisation.
/// </summary>
[CollectionDefinition(Name)]
public class SqlServerIntegrationCollection
{
    public const string Name = "SqlServerIntegration";
}
