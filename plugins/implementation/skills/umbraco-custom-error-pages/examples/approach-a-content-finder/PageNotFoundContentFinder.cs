using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Skills.Examples.CustomErrorPages.ContentFinders;

/// <summary>
/// Finds and displays a custom 404 error page using structural discovery.
/// Supports single-site and multi-site setups:
/// - If a domain is matched on the request, uses that domain's root node.
/// - Falls back to the first root node for single-site setups without domains configured.
/// </summary>
public class PageNotFoundContentFinder : IContentLastChanceFinder
{
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IDocumentNavigationQueryService _documentNavigationQueryService;

    // Replace with your Document Type alias for the 404 error page (e.g., "ErrorPage404")
    private const string ErrorPageAlias = "error";

    public PageNotFoundContentFinder(
        IUmbracoContextFactory umbracoContextFactory,
        IDocumentNavigationQueryService documentNavigationQueryService)
    {
        _umbracoContextFactory = umbracoContextFactory;
        _documentNavigationQueryService = documentNavigationQueryService;
    }

    public Task<bool> TryFindContent(IPublishedRequestBuilder request)
    {
        using UmbracoContextReference contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var contentCache = contextRef.UmbracoContext.Content;
        if (contentCache == null)
            return Task.FromResult(false);

        // For multi-site: use the domain matched on the request to find the correct root node.
        // request.Domain is set by Umbraco's domain routing before IContentLastChanceFinder runs.
        // For single-site without domains configured, fall back to the first root node.
        IPublishedContent? siteRoot = request.Domain?.ContentId is int domainRootId
            ? contentCache.GetById(domainRootId)
            : FirstRoot(contentCache);

        // FirstChildOfType, not FirstChild: FirstChild's single-string overload takes a CULTURE,
        // so passing an alias to it silently returns the first child of any type.
        IPublishedContent? notFoundPage = siteRoot?.FirstChildOfType(ErrorPageAlias);

        if (notFoundPage == null)
            return Task.FromResult(false);

        request.SetPublishedContent(notFoundPage);
        request.SetResponseStatus(404);
        return Task.FromResult(true);
    }

    // The published content cache has no "get root nodes" method: root keys come from the
    // document navigation service, and each key is then resolved through the cache.
    private IPublishedContent? FirstRoot(IPublishedContentCache contentCache) =>
        _documentNavigationQueryService.TryGetRootKeys(out IEnumerable<Guid> rootKeys)
            ? rootKeys.Select(contentCache.GetById).FirstOrDefault(root => root is not null)
            : null;
}

/// <summary>
/// Composer that automatically registers the PageNotFoundContentFinder.
/// </summary>
public class PageNotFoundContentFinderComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.SetContentLastChanceFinder<PageNotFoundContentFinder>();
    }
}
