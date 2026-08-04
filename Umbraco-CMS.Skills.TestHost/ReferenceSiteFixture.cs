using Umbraco_CMS.Skills.TestHost.Shared;

namespace Umbraco_CMS.Skills.TestHost;

/// <summary>
/// Boots site 1 ONCE for the whole test assembly and shares it with every skill fixture.
///
/// This is not just an optimisation. Umbraco keeps process-wide static state, so a second host in
/// the same process leaves skill code resolving services from a dead provider — see
/// UmbracoHostSentinel, which turns that into a named failure instead of a mystery.
///
/// A [SetUpFixture] in this namespace wraps every fixture in it: OneTimeSetUp runs before the
/// first, OneTimeTearDown after the last.
/// </summary>
[SetUpFixture]
public class ReferenceSiteFixture
{
    /// <summary>Identifies this host to the process-wide sentinel.</summary>
    public const string HostName = "Umbraco-CMS.Skills (Clean)";

    /// <summary>The shared instance. Use it to create extra clients (e.g. non-redirecting).</summary>
    public static ReferenceSiteFactory Factory { get; private set; } = null!;

    /// <summary>Default client: installed content ready, redirects followed.</summary>
    public static HttpClient Client { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task BootReferenceSite()
    {
        UmbracoHostSentinel.Claim(HostName);

        Factory = new ReferenceSiteFactory();
        Client = Factory.CreateClient(); // first call boots Umbraco + installs Clean into the test DB

        // Clean's content arrives WITH the install, so "installed" means content exists. Waiting on
        // total > 0 stops the sitemap controller caching an empty urlset mid-install.
        await Factory.WaitUntilInstalledAsync(
            Client,
            root => root.TryGetProperty("total", out System.Text.Json.JsonElement total)
                    && total.GetInt32() > 0);
    }

    [OneTimeTearDown]
    public void ShutDownReferenceSite()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }
}
