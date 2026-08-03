using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace Umbraco.Skills.Examples.CustomErrorPages;

// VALIDATION-ONLY harness — NOT generated from the skill's assets/ and NOT part of what the
// skill ships to users.
//
// The skill's 500 reference (references/500-error-controller.md) asks the user to make two
// changes to their own host: add `app.UseExceptionHandler("/error")` before `app.UseUmbraco()`,
// and add `~/error/` to Umbraco's ReservedPaths. The reference instance is shared by every
// skill example, so instead of editing its Program.cs / appsettings.json we make those same two
// changes from inside this example — an IComposer plus an IUmbracoPipelineFilter, which is how a
// package would do it. If this stops being equivalent to the documented manual steps, the
// skill's reference is what should be trusted.
public class ExampleHostWiringComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Equivalent of the documented `app.UseExceptionHandler("/error")` in Program.cs.
        // OnPrePipeline runs before any Umbraco middleware, so the handler wraps everything that
        // follows it — including the endpoint that renders a page and throws.
        builder.Services.Configure<UmbracoPipelineOptions>(options =>
            options.AddFilter(new UmbracoPipelineFilter(nameof(ExampleHostWiringComposer))
            {
                PrePipeline = app => app.UseExceptionHandler("/error"),
            }));

        // Equivalent of adding ~/error/ to Umbraco:CMS:Global:ReservedPaths in appsettings.json,
        // so /error reaches the skill's ErrorController instead of Umbraco's content routing.
        // PostConfigure appends rather than replaces, keeping the defaults intact.
        // Side effect on the shared instance: Clean's own Error node lives at /error/, so that URL
        // now serves the controller instead of the node. Harmless for the tests (which reach the
        // node through the 404/500 code paths), but worth knowing when poking the site by hand.
        builder.Services.PostConfigure<GlobalSettings>(settings =>
        {
            if (!settings.ReservedPaths.Contains("~/error/", StringComparison.OrdinalIgnoreCase))
            {
                settings.ReservedPaths = settings.ReservedPaths.TrimEnd(',') + ",~/error/";
            }
        });
    }
}

/// <summary>
/// Gives the test suite a request that reliably throws an unhandled exception, so the 500 path
/// can be provoked on demand. Validation-only: the skill itself ships no such endpoint.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
public class ExampleThrowController : Controller
{
    [HttpGet]
    [Route("example/custom-error-pages/throw")]
    public IActionResult Throw() =>
        throw new InvalidOperationException(
            "Deliberate exception from ExampleThrowController, to validate the 500 error page.");
}
