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

        // Wait for the seeded children of the root to be QUERYABLE, not merely for the API to answer.
        //
        // This site's content is created during startup rather than by the install, and the Delivery
        // API's query endpoint reads the Examine index, which is populated asynchronously. So the
        // endpoint starts answering 200 before the seeded content is searchable, and anything reading
        // that query — the content preconditions — sees an empty site and fails. Front-end routing goes
        // through the published cache instead and is already consistent, which is exactly why the
        // symptom looked so selective: every rendering test passed while the preconditions failed.
        await Factory.WaitUntilInstalledAsync(
            Client,
            root => root.TryGetProperty("total", out System.Text.Json.JsonElement total)
                    && total.GetInt32() > 0,
            url: "/umbraco/delivery/api/v2/content?fetch=children:/&take=1");
    }

    [OneTimeTearDown]
    public void ShutDownBlankSite()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }
}
