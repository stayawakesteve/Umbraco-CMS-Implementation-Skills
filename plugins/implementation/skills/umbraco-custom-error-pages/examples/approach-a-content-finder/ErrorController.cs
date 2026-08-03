using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Skills.Examples.CustomErrorPages.Controllers;

/// <summary>
/// Handles 500 Internal Server Error responses by displaying a custom error page.
/// Registered via app.UseExceptionHandler("/error") in Program.cs.
/// Uses structural discovery to find the error page as a child of the root node.
/// </summary>
public class ErrorController : Controller
{
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IDocumentNavigationQueryService _documentNavigationQueryService;
    private readonly ILogger<ErrorController> _logger;

    // Replace with your Document Type alias for the 500 error page (e.g., "ErrorPage500")
    private const string ErrorPageAlias = "error";

    public ErrorController(
        IUmbracoContextFactory umbracoContextFactory,
        IDocumentNavigationQueryService documentNavigationQueryService,
        ILogger<ErrorController> logger)
    {
        _umbracoContextFactory = umbracoContextFactory;
        _documentNavigationQueryService = documentNavigationQueryService;
        _logger = logger;
    }

    [HttpGet]
    [Route("Error")]
    public IActionResult Index()
    {
        // IExceptionHandlerPathFeature is set by UseExceptionHandler middleware when a real
        // unhandled exception occurred. Checking this is reliable across ASP.NET Core versions,
        // unlike Response.StatusCode which may not be set yet at this point in the pipeline.
        var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

        if (exceptionFeature?.Error == null)
        {
            return Redirect("/");
        }

        try
        {
            using UmbracoContextReference contextRef = _umbracoContextFactory.EnsureUmbracoContext();

            // Navigate: Root → First child with matching Document Type alias
            // Same approach as 404 finder - works with any root structure.
            // FirstChildOfType, not FirstChild: FirstChild's single-string overload takes a
            // CULTURE, so passing an alias to it silently returns the first child of any type.
            IPublishedContentCache? contentCache = contextRef.UmbracoContext.Content;
            IPublishedContent? error500Page = contentCache is null
                ? null
                : FirstRoot(contentCache)?.FirstChildOfType(ErrorPageAlias);

            if (error500Page != null)
            {
                Response.StatusCode = StatusCodes.Status500InternalServerError;
                return View(error500Page.GetTemplateAlias(), error500Page);
            }

            // The error page node is missing from the content tree.
            // This is a setup problem - create and publish a node with alias ErrorPageAlias.
            _logger.LogWarning(
                "500 error page node not found. Expected a published content node with Document Type alias '{Alias}' as a direct child of the root node.",
                ErrorPageAlias);
        }
        catch (Exception ex)
        {
            // Umbraco context unavailable - this can happen if the exception occurred
            // during application startup before Umbraco has fully initialised.
            _logger.LogError(ex, "Could not load 500 error page from Umbraco content. Falling back to plain text response.");
        }

        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return Content("Internal Server Error. Please try again later.", "text/html");
    }

    // The published content cache has no "get root nodes" method: root keys come from the
    // document navigation service, and each key is then resolved through the cache.
    private IPublishedContent? FirstRoot(IPublishedContentCache contentCache) =>
        _documentNavigationQueryService.TryGetRootKeys(out IEnumerable<Guid> rootKeys)
            ? rootKeys.Select(contentCache.GetById).FirstOrDefault(root => root is not null)
            : null;
}
