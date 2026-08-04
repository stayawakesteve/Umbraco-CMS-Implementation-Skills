using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Skills.Examples.Fixtures;

namespace Umbraco.Skills.Examples.SitemapApproachB;

/// <summary>
/// This example's own harness — not part of the skill.
///
/// Creates the pages the sitemap should and shouldn't list, as a SUBTREE of site 2's single shared root
/// rather than as a root of its own. Umbraco derives front-end URLs from the tree, so a second root
/// would change every URL on the site and break the other examples' fixtures.
///
/// In code rather than in the package manifest because a manifest can only import documents at the site
/// root. Doing it here also sets the template and publish state explicitly, which avoids two traps: a
/// manifest can only reference a template by unstable numeric id, and package import saves content
/// without ever publishing it.
///
/// Runs on UmbracoApplicationStarted, which is after all package migrations, so the shared root and this
/// example's Document Types both exist by then. Idempotent, because the test database is reused across
/// boots and because other examples seed their own subtrees on the same signal.
/// </summary>
public class ExampleContentSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IContentPublishingService _publishingService;
    private readonly IDocumentUrlService _documentUrlService;
    private readonly IDatabaseCacheRebuilder _cacheRebuilder;
    private readonly ILogger<ExampleContentSeeder> _logger;

    public ExampleContentSeeder(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IContentPublishingService publishingService,
        IDocumentUrlService documentUrlService,
        IDatabaseCacheRebuilder cacheRebuilder,
        ILogger<ExampleContentSeeder> logger)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _publishingService = publishingService;
        _documentUrlService = documentUrlService;
        _cacheRebuilder = cacheRebuilder;
        _logger = logger;
    }

    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        IContent? root = FixtureSite.FindRoot(_contentService);
        if (root is null)
        {
            // Say so rather than failing silently: the fixtures would otherwise report a missing sitemap
            // and send the reader looking at the skill instead of at the host.
            _logger.LogWarning(
                "Site 2 has no root node, so the sitemap Approach B fixture content was not seeded.");
            return;
        }

        // Each page covers one branch of the template's filtering.
        FixtureSite.EnsureChild(_contentService, _contentTypeService, root, "examplePage", "Visible page",
            new Dictionary<string, object?>
            {
                ["xmlSiteMapPriority"] = "0.8",
                ["xmlSiteMapChangeFrequency"] = "weekly",
            });

        FixtureSite.EnsureChild(_contentService, _contentTypeService, root, "examplePage", "Hidden page",
            new Dictionary<string, object?> { ["hideFromXmlSiteMap"] = true });

        // Not composed with xmlSiteMapSettings at all — proves the template's HasProperty short-circuit
        // keeps types the composition was never applied to, as well as excludedDocumentTypes filtering.
        FixtureSite.EnsureChild(_contentService, _contentTypeService, root, "excludedPage",
            "Excluded type page");

        FixtureSite.EnsureChild(_contentService, _contentTypeService, root, "xmlSiteMap", "XML Sitemap",
            new Dictionary<string, object?> { ["excludedDocumentTypes"] = "excludedPage,xmlSiteMap" });

        await FixtureSite.PublishAndRefreshAsync(
            _publishingService, _documentUrlService, _cacheRebuilder, root);
    }
}

/// <summary>Registers the seeder. Picked up by AddComposers() in the blank host's Program.cs.</summary>
public class ExampleHostWiringComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, ExampleContentSeeder>();
}
