using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Umbraco_CMS.Skills.TestHost.Shared;

/// <summary>
/// Boots one of the reference hosts in-process via a TestServer, so fixtures can assert skill code
/// over real HTTP with no external server and no LLM. Deterministic: the verdict is the assertion
/// outcome.
///
/// Shared by both hosts because they differ in only two things — which assembly they boot, and
/// which database file they own. Everything else (Development environment so
/// appsettings.Development.json applies, isolated test DB, unattended install, the wait for the
/// install to finish) is identical, and drift between two copies of it would be silent.
///
/// The host installs unattended into an ISOLATED test SQLite DB, separate from the dev instance's,
/// so a test run never touches a database someone is poking at by hand.
/// </summary>
public abstract class SkillSiteFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    /// <summary>
    /// File name of this host's test database. MUST be unique per host: two hosts sharing one
    /// SQLite file would interleave two different content shapes into the same tables.
    /// </summary>
    protected abstract string DatabaseFileName { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development picks up appsettings.Development.json (unattended install + SQLite).
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] =
                    $"Data Source=|DataDirectory|/{DatabaseFileName};Cache=Shared;Foreign Keys=True;Pooling=True",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
                ["Umbraco:CMS:Unattended:InstallUnattended"] = "true",
                ["Umbraco:CMS:Unattended:UnattendedUserName"] = "Administrator",
                ["Umbraco:CMS:Unattended:UnattendedUserEmail"] = "admin@example.com",
                ["Umbraco:CMS:Unattended:UnattendedUserPassword"] = "1234567890",
            });
        });
    }

    /// <summary>
    /// Blocks until the unattended install has finished, so tests don't race a still-installing
    /// site. Gates on the Delivery API rather than on the endpoint under test: a request made
    /// mid-install can be cached (the sitemap controller caches its result), so an early read would
    /// pin an empty response for the rest of the run.
    /// </summary>
    /// <param name="ready">
    /// Extra condition on the Delivery API's response body, checked once the endpoint answers 200.
    /// A host whose content arrives with the install (Clean) should require total &gt; 0. A host
    /// whose content is seeded by a package migration during boot needs no extra condition — the
    /// migration has already run by the time anything is served.
    /// </param>
    /// <param name="url">
    /// Which Delivery API query to poll. Defaults to a bare content query, but a host whose content is
    /// seeded during startup should poll the shape its fixtures actually depend on — see the override in
    /// BlankSiteFixture.
    /// </param>
    public async Task WaitUntilInstalledAsync(
        HttpClient client,
        Func<JsonElement, bool>? ready = null,
        TimeSpan? timeout = null,
        string url = "/umbraco/delivery/api/v2/content?take=1")
    {
        TimeSpan budget = timeout ?? TimeSpan.FromMinutes(4);
        DateTime deadline = DateTime.UtcNow + budget;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    using JsonDocument doc =
                        JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (ready is null || ready(doc.RootElement))
                    {
                        return;
                    }
                }
            }
            catch (HttpRequestException)
            {
                // site still booting — retry
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new TimeoutException(
            $"{typeof(TEntryPoint).Assembly.GetName().Name} did not finish installing within "
            + $"{budget.TotalSeconds:n0}s.");
    }
}
