using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Skills.Examples.Fixtures;

namespace Umbraco.Skills.Examples.CustomErrorPagesApproachB;

/// <summary>
/// This example's own harness — not part of the skill.
///
/// Stands in for the two manual steps that make Approach B work, and which no package manifest can do
/// for a user: publish a node of the ErrorPage404 type, and put THAT node's GUID into
/// Umbraco:CMS:Content:Error404Collection.
/// </summary>
public static class ErrorPageFixture
{
    /// <summary>
    /// The 404 node's key, pinned so the config below can name it.
    ///
    /// A real user copies this GUID off the node's Info tab and pastes it into appsettings.json, which is
    /// the one genuinely awkward part of Approach B: the value is per-environment content, so every
    /// environment either shares content or needs its own entry. Pinning it here is the deterministic
    /// equivalent of that copy-paste.
    /// </summary>
    public static readonly Guid ErrorPageKey = new("d1000000-0000-4000-8000-0000000000b1");

    /// <summary>Rendered marker the fixtures assert on, so a 404 body can be told apart from any other.</summary>
    public const string PageTitle = "Page not found (Approach B)";
}

/// <summary>
/// Creates and publishes the 404 node as a child of site 2's single shared root.
///
/// Runs on UmbracoApplicationStarted, which is after all package migrations, so the shared root and the
/// ErrorPage404 type both exist by then. Idempotent — the test database is reused across boots.
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
            // Say so rather than failing silently: the fixtures would otherwise report a plain 404 with
            // no marker and send the reader looking at the finder instead of at the missing content.
            _logger.LogWarning(
                "Site 2 has no root node, so the error-pages Approach B 404 node was not seeded.");
            return;
        }

        FixtureSite.EnsureChild(
            _contentService, _contentTypeService, root, "errorPage404", "Not found",
            new Dictionary<string, object?> { ["pageTitle"] = ErrorPageFixture.PageTitle },
            key: ErrorPageFixture.ErrorPageKey);

        await FixtureSite.PublishAndRefreshAsync(
            _publishingService, _documentUrlService, _cacheRebuilder, root);
    }
}

/// <summary>
/// The config half of Approach B — the appsettings.json edit, applied in code.
///
/// This is the whole of the approach's wiring: Umbraco's built-in ContentFinderByConfigured404 reads
/// Error404Collection through IOptionsMonitor and resolves the node itself, so there is no custom finder,
/// no composer registration, and no C# for a user to copy. Which is exactly why Approach B is worth
/// offering — and why it cannot coexist with Approach A, whose SetContentLastChanceFinder REPLACES that
/// very finder.
///
/// Culture is "default": with no domains configured the finder falls back to the entry whose Culture is
/// literally "default" (NotFoundHandlerHelper — "there should be a default one!"). ContentErrorPage also
/// requires exactly one of ContentId / ContentKey, so setting the key alone is correct.
///
/// PostConfigure rather than Configure, and it APPENDS: overwriting would throw away any entry that came
/// from appsettings.json, which is where a real user's would be.
/// </summary>
public class ExampleHostWiringComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, ExampleContentSeeder>();

        builder.Services.PostConfigure<ContentSettings>(settings =>
            settings.Error404Collection =
            [
                .. settings.Error404Collection,
                new ContentErrorPage
                {
                    Culture = "default",
                    ContentKey = ErrorPageFixture.ErrorPageKey,
                },
            ]);
    }
}
