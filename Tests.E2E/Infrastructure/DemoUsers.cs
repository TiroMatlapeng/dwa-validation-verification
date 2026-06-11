namespace dwa_ver_val.E2E.Infrastructure;

/// <summary>
/// The demo-user emails seeded by <c>IdentitySeeder</c> when
/// <c>Identity:InitialDemoPassword</c> is set. The WMA-scoped roles embed the
/// lower-cased WmaCode of the seeded Regional OrganisationalUnit — discover it at
/// runtime (do NOT hardcode) via <see cref="E2EAppFixture"/> consumers that read the DB.
/// </summary>
public static class DemoUsers
{
    public const string Admin = "admin@dwa.demo";
    public const string National = "national@dwa.demo";
    public const string ReadOnly = "readonly@dwa.demo";

    public static string Regional(string wmaCode) => $"regional-{wmaCode}@dwa.demo";
    public static string Validator(string wmaCode) => $"validator-{wmaCode}@dwa.demo";
    public static string Capturer(string wmaCode) => $"capturer-{wmaCode}@dwa.demo";
}
