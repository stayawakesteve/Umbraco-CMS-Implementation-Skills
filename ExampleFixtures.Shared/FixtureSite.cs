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
/// Deliberately only static helpers. Umbraco DISCOVERS types by scanning assemblies, so an IComposer, a
/// PackageMigrationPlan or a notification handler living in a project this widely referenced would be
/// picked up from here as well as from its owner. Discoverable types belong in exactly one host or
/// example project; this one stays inert.
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
    ///
    /// Pass <paramref name="key"/> to pin the node's GUID when something outside the tree refers to it.
    /// </summary>
    public static IContent EnsureChild(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IContent parent,
        string contentTypeAlias,
        string name,
        IDictionary<string, object?>? values = null,
        Guid? key = null)
    {
        // The shorter GetPagedChildren overload is obsolete (removed in Umbraco 19), so pass every
        // parameter explicitly.
        IContent? existing = contentService
            .GetPagedChildren(parent.Id, 0, 100, out _, propertyAliases: null, filter: null,
                ordering: null)
            .FirstOrDefault(c => c.Name == name);

        if (existing is not null)
        {
            return existing;
        }

        IContent created = contentService.Create(name, parent.Id, contentTypeAlias);

        // A caller can pin the key when something outside the content tree has to name this node —
        // error-pages Approach B puts the 404 node's GUID in appsettings, which is exactly what a real
        // user does by copying it off the node's Info tab.
        if (key is not null)
        {
            created.Key = key.Value;
        }

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
