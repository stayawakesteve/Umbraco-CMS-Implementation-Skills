using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Umbraco.Skills.Examples.Fixtures;

/// <summary>
/// Helpers for seeding fixture content on SITE 2, the reference host with no starter kit.
///
/// Site 2 has exactly ONE root node, owned by the host. Every content-shaped example seeds its own
/// subtree beneath it rather than creating a root of its own — because Umbraco derives front-end URLs
/// from the tree, and with two or more roots and no domain configuration every URL changes shape. One
/// example adding a root would silently break every other example's fixtures.
///
/// Deliberately only static helpers: this file is LINKED into the host and into each content-shaped
/// example, so anything Umbraco discovers by type (an IComposer, a PackageMigrationPlan, a notification
/// handler) would be found once per assembly and run several times, or collide on a duplicate plan
/// name. Discoverable types belong in exactly one project.
/// </summary>
public static class FixtureSite
{
    /// <summary>
    /// Site 2's single root, found structurally rather than by a shared constant — the same way skill
    /// code finds the site root, and it keeps examples from having to reference the host.
    /// </summary>
    public static IContent? FindRoot(IContentService contentService) =>
        contentService.GetRootContent().OrderBy(c => c.SortOrder).FirstOrDefault();

    /// <summary>
    /// Creates a child of <paramref name="parent"/> if one of that name doesn't already exist, applying
    /// the content type's default template. Returns the node either way, so callers are idempotent
    /// across reboots — the test database is reused.
    ///
    /// The template has to be set explicitly: package manifests can only reference a template by
    /// numeric id, so examples omit it, and a published node with no template doesn't render — Umbraco
    /// answers 404, which looks exactly like missing content.
    /// </summary>
    public static IContent EnsureChild(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IContent parent,
        string contentTypeAlias,
        string name,
        IDictionary<string, object?>? values = null)
    {
        IContent? existing = contentService
            .GetPagedChildren(parent.Id, 0, 100, out _)
            .FirstOrDefault(c => c.Name == name);

        if (existing is not null)
        {
            return existing;
        }

        IContent created = contentService.Create(name, parent.Id, contentTypeAlias);

        IContentType? contentType = contentTypeService.Get(contentTypeAlias);
        if (contentType?.DefaultTemplate is not null)
        {
            created.TemplateId = contentType.DefaultTemplate.Id;
        }

        if (values is not null)
        {
            foreach ((string alias, object? value) in values)
            {
                created.SetValue(alias, value);
            }
        }

        contentService.Save(created);
        return created;
    }

    /// <summary>
    /// Publishes the whole tree from <paramref name="root"/> and refreshes what front-end routing reads.
    ///
    /// Three separate things, all required, and each fails differently if skipped:
    ///   - Umbraco's package importer SAVES content but never publishes it (its own source has the
    ///     publish call commented out), and unpublished content is invisible everywhere.
    ///   - The document-URL map is PERSISTED and built once during startup, so content published after
    ///     that is unroutable while looking perfectly healthy in the content services and Delivery API.
    ///   - The published content cache likewise needs rebuilding, or the route resolves to nothing.
    ///
    /// Nothing runs on a background thread: fixtures assert as soon as boot returns, and a race here
    /// would surface as empty or missing output rather than as a failure to publish.
    ///
    /// Safe to call from several examples on one boot — publishing an already-published branch and
    /// rebuilding an up-to-date cache are both no-ops, which is what lets examples stay independent of
    /// each other's ordering.
    /// </summary>
    public static async Task PublishAndRefreshAsync(
        IContentPublishingService publishingService,
        IDocumentUrlService documentUrlService,
        IDatabaseCacheRebuilder cacheRebuilder,
        IContent root)
    {
        Attempt<ContentPublishingBranchResult, ContentPublishingOperationStatus> result =
            await publishingService.PublishBranchAsync(
                root.Key,
                ["*"],
                PublishBranchFilter.IncludeUnpublished,
                Constants.Security.SuperUserKey,
                useBackgroundThread: false);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Publishing the site 2 fixture branch from '{root.Name}' failed with {result.Status}. "
                + "Every content-shaped example's fixtures depend on it.");
        }

        await documentUrlService.RebuildAllUrlsAsync();
        await cacheRebuilder.RebuildAsync(useBackgroundThread: false);
    }
}
