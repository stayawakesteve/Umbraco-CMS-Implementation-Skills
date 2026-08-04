using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco_CMS.Skills.TestHost;

/// <summary>
/// Proves the umbraco-sitemap skill's SitemapCacheInvalidator actually works.
///
/// The controller caches its rendered XML with no expiry, so without a working invalidator the
/// sitemap would go stale the moment content changed — and every other test would still pass,
/// because they only ever read the first cached response. The composer registers the notification
/// handler, but nothing else in the suite publishes anything to fire it.
///
/// ORDER MATTERS HERE, and getting it wrong made earlier versions of this test flaky (~1 run in 5).
/// Publishing clears the cache immediately, but the published cache updates asynchronously, so a
/// sitemap request made too early rebuilds from pre-change content and re-caches it. With no
/// expiry on the entry, that stale XML then survives until the next publish — so a test that reads
/// /sitemap.xml too soon poisons the very thing it is about to assert on, and fails blaming the
/// invalidator.
///
/// So: don't touch /sitemap.xml until the controller's own data source reflects the change. See
/// WaitForSitemapSource — matching that traversal exactly is what made this reliable, and polling
/// something merely adjacent to it (a key lookup, or the Delivery API) is what did not.
///
/// If it ever goes intermittent again, the fast way to tell invalidation from staleness is to log
/// from both sides of the cache: a "served from cache" hit means the handler didn't clear it, while
/// a fresh rebuild that still contains the changed page means the wait above returned too early.
/// A DI mismatch is already ruled out — controller and handler were observed sharing one
/// IMemoryCache instance.
///
/// Content is changed and put back, so the restore runs in a finally: the other sitemap
/// assertions use the same shared instance.
/// </summary>
[TestFixture]
// Unpublishes and republishes a node that other fixtures READ. NUnit runs sequentially by
// default, so today that's safe by accident; this makes the requirement explicit and survives
// someone adding [assembly: Parallelizable] later.
[NonParallelizable]
public class SitemapCacheInvalidationTests
{
    private static readonly XNamespace Sm = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>
    /// A Clean node no other fixture asserts on. Deliberately NOT "Features", which SitemapTests
    /// checks for — mutating that would couple the two fixtures together.
    /// </summary>
    private const string NodeName = "About";

    private static HttpClient Client => ReferenceSiteFixture.Client;

    private static T Resolve<T>() where T : notnull =>
        ReferenceSiteFixture.Factory.Services.GetRequiredService<T>();

    private static async Task<List<string>> GetSitemapLocationsAsync()
    {
        HttpResponseMessage response = await Client.GetAsync("/sitemap.xml");
        XDocument doc = XDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.Descendants(Sm + "loc").Select(e => e.Value).ToList();
    }

    /// <summary>
    /// Blocks until the node has (or hasn't) appeared in the exact traversal SitemapController
    /// uses — root.DescendantsOrSelf() over the published cache.
    ///
    /// Matching the traversal matters. An earlier version polled Content.GetById(key) instead, on
    /// the assumption that "not resolvable by key" implies "not in the tree". Those are different
    /// paths through the published cache and they don't necessarily update together, so the test
    /// could proceed while DescendantsOrSelf() still yielded the node — the controller would then
    /// legitimately rebuild XML containing it, and the test would fail blaming the invalidator.
    /// Polling what the controller actually reads removes that gap. (The Delivery API is
    /// unsuitable for the same class of reason, only more so: it's a separate read model.)
    /// </summary>
    private static void WaitForSitemapSource(Guid key, bool shouldBePresent)
    {
        IUmbracoContextFactory contextFactory = Resolve<IUmbracoContextFactory>();
        IDocumentNavigationQueryService navigation = Resolve<IDocumentNavigationQueryService>();
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            using UmbracoContextReference ctx = contextFactory.EnsureUmbracoContext();

            bool isPresent = false;
            if (navigation.TryGetRootKeys(out IEnumerable<Guid> rootKeys))
            {
                IPublishedContent? root = rootKeys
                    .Select(k => ctx.UmbracoContext.Content?.GetById(k))
                    .FirstOrDefault(r => r is not null);

                isPresent = root?.DescendantsOrSelf().Any(x => x.Key == key) ?? false;
            }

            if (isPresent == shouldBePresent)
            {
                return;
            }

            Thread.Sleep(100);
        }

        Assert.Fail("the published tree never reported the node as "
            + $"{(shouldBePresent ? "present" : "absent")} — the content change itself did not take "
            + "effect, so nothing can be concluded about cache invalidation");
    }

    [Test]
    public async Task Unpublishing_content_invalidates_the_cached_sitemap()
    {
        IContentService contentService = Resolve<IContentService>();

        IContent node = contentService.GetRootContent()
            .SelectMany(root => contentService.GetPagedDescendants(root.Id, 0, 100, out _))
            .Concat(contentService.GetRootContent())
            .First(c => c.Name == NodeName);

        // Warm the cache while the node is still published, so a failure to invalidate leaves
        // provably stale XML behind. Without this the test could pass on a cold cache.
        List<string> before = await GetSitemapLocationsAsync();
        string? target = before.FirstOrDefault(loc => loc.Contains($"/{NodeName.ToLowerInvariant()}/"));
        Assert.That(target, Is.Not.Null,
            $"expected Clean's '{NodeName}' page in the sitemap before unpublishing — "
            + $"got: {string.Join(", ", before)}");

        try
        {
            contentService.Unpublish(node);
            WaitForSitemapSource(node.Key, shouldBePresent: false);

            // First sitemap request since the change, on a settled snapshot.
            List<string> after = await GetSitemapLocationsAsync();
            Assert.That(after, Has.None.EqualTo(target),
                $"'{target}' is still listed, so the pre-unpublish XML was served from cache — "
                + "SitemapCacheInvalidator did not clear it on ContentUnpublishedNotification");
        }
        finally
        {
            contentService.Publish(node, ["*"]);
        }

        WaitForSitemapSource(node.Key, shouldBePresent: true);

        List<string> restored = await GetSitemapLocationsAsync();
        Assert.That(restored, Has.Member(target),
            $"'{target}' did not come back, so the cache was not cleared on "
            + "ContentPublishedNotification either");
    }
}
