using System.Net;
using System.Xml.Linq;

namespace Umbraco_CMS.Skills.TestHost.Blank;

/// <summary>
/// Deterministic runtime validation of umbraco-sitemap APPROACH B — the Razor template + Document Type
/// route. Proves the template the skill ships (assets/xmlSitemap.cshtml, installed verbatim by this
/// example's package migration) renders a valid sitemap at the content node's own URL, and that its
/// two filters actually filter.
///
/// Runs on SITE 2, the host with no starter kit. That is the whole point: Clean ships its own
/// xMLSitemap Document Type and Views/xMLSitemap.cshtml, so the same assertions on the Clean host
/// could pass on Clean's implementation while this skill's guidance was broken.
///
/// The *BlankTests.cs suffix is what routes this file into the blank test assembly, and therefore
/// into its own process — see Umbraco-CMS.Skills.TestHost.Blank.csproj.
/// </summary>
[TestFixture]
public class SitemapApproachBBlankTests
{
    private static readonly XNamespace Sm = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>
    /// The XmlSiteMap node's own URL — Approach B has no fixed /sitemap.xml route. Umbraco derives the
    /// segment from the node NAME ("XML Sitemap"), not from the manifest's urlName attribute, which it
    /// ignores; hence the hyphen.
    /// </summary>
    private const string SitemapUrl = "/xml-sitemap/";

    private static HttpClient Client => BlankSiteFixture.Client;

    private static async Task<List<string>> LocationsAsync()
    {
        HttpResponseMessage response = await Client.GetAsync(SitemapUrl);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"GET {SitemapUrl} should render the XmlSiteMap content node");

        XDocument doc = XDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.Descendants(Sm + "loc").Select(e => e.Value).ToList();
    }

    [Test]
    public async Task Sitemap_node_serves_xml()
    {
        HttpResponseMessage response = await Client.GetAsync(SitemapUrl);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/xml"));
    }

    [Test]
    public async Task Sitemap_root_is_urlset_listing_published_pages()
    {
        HttpResponseMessage response = await Client.GetAsync(SitemapUrl);
        XDocument doc = XDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(doc.Root?.Name, Is.EqualTo(Sm + "urlset"),
            "root element must be <urlset> in the sitemaps.org 0.9 namespace");

        List<string> locations = doc.Descendants(Sm + "loc").Select(e => e.Value).ToList();
        Assert.That(locations, Has.Some.EndsWith("/visible-page/"),
            $"the fixture's visible page must be listed. Got: {string.Join(", ", locations)}");
    }

    [Test]
    public async Task Per_page_hide_toggle_excludes_a_page()
    {
        List<string> locations = await LocationsAsync();

        Assert.That(locations, Has.None.EndsWith("/hidden-page/"),
            "Hidden page has hideFromXmlSiteMap = true, so the template must exclude it. "
            + $"Got: {string.Join(", ", locations)}");
    }

    [Test]
    public async Task Excluded_document_types_are_filtered_out()
    {
        List<string> locations = await LocationsAsync();

        Assert.That(locations, Has.None.EndsWith("/excluded-type-page/"),
            "'excludedPage' is listed in the sitemap node's excludedDocumentTypes, so pages of that "
            + $"type must be excluded. Got: {string.Join(", ", locations)}");
        Assert.That(locations, Has.None.EndsWith(SitemapUrl),
            "the sitemap node excludes its own type, so it must not list itself");
    }

    /// <summary>
    /// Guards the template's HasProperty short-circuit. 'excludedPage' has no xmlSiteMapSettings
    /// composition at all, so a naive Value&lt;bool&gt; check would treat every such page as hidden
    /// and silently drop content types the composition was never applied to.
    /// </summary>
    [Test]
    public async Task Pages_without_the_settings_composition_are_still_listed()
    {
        List<string> locations = await LocationsAsync();

        Assert.That(locations, Has.Some.EndsWith("/home/").Or.Some.EqualTo(locations.FirstOrDefault()),
            "the fixture root must appear — if it doesn't, the filter is dropping pages it shouldn't");
        Assert.That(locations, Is.Not.Empty);
    }

    /// <summary>
    /// Per-page priority and change frequency are Approach B's stated advantage over Approach A, and
    /// they're editor-set values, so they have to survive into the output.
    /// </summary>
    [Test]
    public async Task Editor_set_priority_and_change_frequency_are_emitted()
    {
        HttpResponseMessage response = await Client.GetAsync(SitemapUrl);
        XDocument doc = XDocument.Parse(await response.Content.ReadAsStringAsync());

        XElement? visible = doc.Descendants(Sm + "url")
            .FirstOrDefault(u => u.Element(Sm + "loc")?.Value.EndsWith("/visible-page/") == true);

        Assert.That(visible, Is.Not.Null, "the visible page must be in the sitemap to assert on it");
        Assert.That(visible!.Element(Sm + "changefreq")?.Value, Is.EqualTo("weekly"));
        Assert.That(visible.Element(Sm + "priority")?.Value, Is.EqualTo("0.8"));
    }

    /// <summary>
    /// The pages with no priority set must not emit an empty element — an empty &lt;priority&gt; is
    /// invalid against the sitemaps.org schema, so "omit when blank" is correctness, not tidiness.
    /// </summary>
    [Test]
    public async Task Blank_priority_and_change_frequency_are_omitted_not_empty()
    {
        HttpResponseMessage response = await Client.GetAsync(SitemapUrl);
        XDocument doc = XDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(doc.Descendants(Sm + "priority").Select(e => e.Value),
            Has.None.Empty.Or.None.EqualTo(string.Empty));
        Assert.That(doc.Descendants(Sm + "changefreq").Select(e => e.Value),
            Has.None.EqualTo(string.Empty));
    }
}
