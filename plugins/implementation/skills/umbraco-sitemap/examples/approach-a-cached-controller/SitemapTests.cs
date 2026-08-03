using System.Net;
using System.Xml.Linq;

namespace Umbraco_CMS.Skills.TestHost;

/// <summary>
/// Deterministic runtime validation of the umbraco-sitemap skill (Approach A). Proves the
/// skill's SitemapController — compiled from its example project and loaded into the reference
/// instance — actually serves a valid sitemap for the Clean starter kit's published content.
/// </summary>
[TestFixture]
public class SitemapTests
{
    private static readonly XNamespace Sm = "http://www.sitemaps.org/schemas/sitemap/0.9";

    // Shared, already-installed host — see ReferenceSiteFixture. Booting a second host in the same
    // process would leave this fixture resolving services from a disposed provider. The wait for
    // the install also happens there, so the sitemap can't cache an empty urlset.
    private static HttpClient Client => ReferenceSiteFixture.Client;

    [Test]
    public async Task Get_sitemap_returns_ok_and_xml_content_type()
    {
        HttpResponseMessage response = await Client.GetAsync("/sitemap.xml");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/xml"));
    }

    [Test]
    public async Task Get_sitemap_root_is_urlset_listing_published_pages()
    {
        HttpResponseMessage response = await Client.GetAsync("/sitemap.xml");
        string body = await response.Content.ReadAsStringAsync();
        XDocument doc = XDocument.Parse(body);

        Assert.That(doc.Root?.Name, Is.EqualTo(Sm + "urlset"),
            "root element must be <urlset> in the sitemaps.org 0.9 namespace");

        List<string> locations = doc.Descendants(Sm + "loc").Select(e => e.Value).ToList();
        Assert.That(locations, Is.Not.Empty, "sitemap should list the Clean starter kit's published pages");
        Assert.That(locations, Has.Some.Contains("/features/"),
            "expected a known Clean node (Features) to appear in the sitemap");
    }
}
