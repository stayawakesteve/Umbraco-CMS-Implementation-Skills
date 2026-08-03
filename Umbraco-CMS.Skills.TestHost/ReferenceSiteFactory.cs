using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Umbraco_CMS.Skills.TestHost;

/// <summary>
/// Boots the reference instance (Umbraco-CMS.Skills, with every skill example project
/// referenced) in-process via a TestServer, so tests can assert skill controllers over HTTP
/// with no external server and no LLM. Deterministic: verdict is the assertion outcome.
///
/// The instance installs unattended into an ISOLATED test SQLite DB (separate from the dev
/// instance's DB); Clean's content is imported on first boot, then the DB is reused.
/// </summary>
public sealed class ReferenceSiteFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development picks up appsettings.Development.json (unattended install + SQLite).
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Dedicated test DB so runs never touch the dev instance's database.
                ["ConnectionStrings:umbracoDbDSN"] =
                    "Data Source=|DataDirectory|/Umbraco.Tests.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
                ["Umbraco:CMS:Unattended:InstallUnattended"] = "true",
                ["Umbraco:CMS:Unattended:UnattendedUserName"] = "Administrator",
                ["Umbraco:CMS:Unattended:UnattendedUserEmail"] = "admin@example.com",
                ["Umbraco:CMS:Unattended:UnattendedUserPassword"] = "1234567890",
            });
        });
    }

    /// <summary>
    /// Blocks until the unattended install has finished importing content, so tests don't race a
    /// still-installing site. Gates on the Delivery API's content count rather than on the endpoint
    /// under test — the sitemap controller caches its result, so a request made mid-install would
    /// otherwise cache an empty urlset. Call once in OneTimeSetUp before asserting.
    /// </summary>
    public async Task WaitUntilContentInstalledAsync(HttpClient client, TimeSpan? timeout = null)
    {
        TimeSpan budget = timeout ?? TimeSpan.FromMinutes(4);
        DateTime deadline = DateTime.UtcNow + budget;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                HttpResponseMessage response =
                    await client.GetAsync("/umbraco/delivery/api/v2/content?take=1");
                if (response.IsSuccessStatusCode)
                {
                    using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (doc.RootElement.TryGetProperty("total", out JsonElement total) &&
                        total.GetInt32() > 0)
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
            $"Reference instance did not finish installing content within {budget.TotalSeconds:n0}s.");
    }
}
