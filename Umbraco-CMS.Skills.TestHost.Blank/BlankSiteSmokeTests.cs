using System.Net;
using System.Text.Json;

namespace Umbraco_CMS.Skills.TestHost.Blank;

/// <summary>
/// Guards site 2's defining property rather than any one skill: this host must boot, and it must
/// have NO starter kit.
///
/// The second assertion is the load-bearing one. If Clean (or any starter kit) ever appears here,
/// every Approach-B fixture silently becomes untrustworthy — Clean ships its own xMLSitemap Document
/// Type with a template, and its own `error` type with a template, which are competing
/// implementations of the very features these fixtures assert. A test could then pass on Clean's
/// implementation while the skill's guidance was broken, and nothing would say so.
/// </summary>
[TestFixture]
public class BlankSiteSmokeTests
{
    private static HttpClient Client => BlankSiteFixture.Client;

    [Test]
    public async Task Site_boots_and_serves_the_delivery_api()
    {
        HttpResponseMessage response = await Client.GetAsync("/umbraco/delivery/api/v2/content?take=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "the blank host should install unattended and serve the Delivery API");
    }

    [Test]
    public async Task No_starter_kit_content_types_are_installed()
    {
        HttpResponseMessage response =
            await Client.GetAsync("/umbraco/delivery/api/v2/content?take=100");
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        string[] contentTypes = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("contentType").GetString() ?? string.Empty)
            .ToArray();

        // Clean's types. Any of these means a starter kit got installed into site 2 and the
        // Approach-B fixtures are no longer proving what they claim to prove.
        foreach (string cleanType in new[] { "home", "content", "article", "xMLSitemap", "error" })
        {
            Assert.That(contentTypes, Has.None.EqualTo(cleanType),
                $"'{cleanType}' is a Clean starter kit Document Type. Site 2 must have no starter "
                + $"kit — its whole purpose is that the only content shape present is the one a "
                + $"skill's own package migration created. Found: {string.Join(", ", contentTypes)}");
        }
    }
}
