using Umbraco_CMS.Skills.TestHost.Shared;

namespace Umbraco_CMS.Skills.TestHost.Blank;

/// <summary>
/// Boots site 2 ONCE for the whole test assembly and shares it with every Approach-B fixture.
///
/// Same constraint as site 1's fixture: one Umbraco host per process. This assembly exists as a
/// SEPARATE test project precisely so it gets a separate process from the Clean host — see
/// UmbracoHostSentinel for what goes wrong otherwise, and why CI runs the two projects as separate
/// `dotnet test` invocations rather than trusting the runner to isolate them.
/// </summary>
[SetUpFixture]
public class BlankSiteFixture
{
    /// <summary>Identifies this host to the process-wide sentinel.</summary>
    public const string HostName = "Umbraco-CMS.Skills.Blank (no starter kit)";

    /// <summary>The shared instance. Use it to create extra clients (e.g. non-redirecting).</summary>
    public static BlankSiteFactory Factory { get; private set; } = null!;

    /// <summary>Default client: site installed and package migrations applied, redirects followed.</summary>
    public static HttpClient Client { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task BootBlankSite()
    {
        UmbracoHostSentinel.Claim(HostName);

        Factory = new BlankSiteFactory();
        Client = Factory.CreateClient();

        // No content-count condition here, deliberately. This site has no starter kit, so a freshly
        // installed host legitimately has zero content until an example's package migration seeds
        // it — and those run during boot, before anything is served. Requiring total > 0 would
        // therefore either hang on a host with no examples yet, or assert something the wait gate
        // isn't responsible for. Each fixture asserts its own content instead.
        await Factory.WaitUntilInstalledAsync(Client);
    }

    [OneTimeTearDown]
    public void ShutDownBlankSite()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }
}
