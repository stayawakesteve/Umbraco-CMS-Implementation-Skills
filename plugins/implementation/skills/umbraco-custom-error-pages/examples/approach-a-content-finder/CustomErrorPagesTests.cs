using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Umbraco_CMS.Skills.TestHost;

/// <summary>
/// Deterministic runtime validation of the umbraco-custom-error-pages skill — Approach A for 404s
/// (PageNotFoundContentFinder) and the shared 500 controller (ErrorController), compiled from the
/// skill's example project and loaded into the reference instance.
///
/// Both assets navigate "site root → first child of Document Type alias X". Clean ships exactly
/// that shape: an "Error" node of type alias 'error' as a direct child of Home, titled
/// "Page not found" — which is what the example resolves <ErrorPageAlias> to, and what these
/// tests assert was actually rendered.
/// </summary>
[TestFixture]
public class CustomErrorPagesTests
{
    /// <summary>Text from Clean's Error node — proves the Umbraco page rendered, not a bare status.</summary>
    private const string ErrorPageMarker = "Page not found";

    /// <summary>ErrorController's last-resort output when it cannot resolve the error page node.</summary>
    private const string PlainTextFallback = "Internal Server Error. Please try again later.";

    // Shared, already-installed host — see ReferenceSiteFixture (the error page node must exist
    // before these assertions, and a second host in the same process breaks other fixtures).
    private static HttpClient Client => ReferenceSiteFixture.Client;

    private HttpClient _noRedirectClient = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() =>
        // An extra client off the shared host — cheap, and redirects must be observable rather
        // than followed for the bare-/error case.
        _noRedirectClient = ReferenceSiteFixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [OneTimeTearDown]
    public void OneTimeTearDown() => _noRedirectClient?.Dispose();

    [Test]
    public async Task Unknown_url_returns_404_and_renders_the_error_page()
    {
        HttpResponseMessage response = await Client.GetAsync("/no-such-page-exists-here");
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
            "the content finder must set a 404 status, not redirect or return 200");
        Assert.That(body, Does.Contain(ErrorPageMarker),
            "expected the resolved Umbraco error page to be rendered");
    }

    [Test]
    public async Task Unhandled_exception_returns_500_and_renders_the_error_page()
    {
        // The example's throw endpoint stands in for a real unhandled exception (e.g. a template
        // calling a missing property), which is what UseExceptionHandler("/error") intercepts.
        HttpResponseMessage response = await Client.GetAsync("/example/custom-error-pages/throw");
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(body, Does.Not.Contain(PlainTextFallback),
            "falling back to plain text means the controller could not resolve the error page node");
        Assert.That(body, Does.Contain(ErrorPageMarker),
            "expected the resolved Umbraco error page to be rendered");
    }

    [Test]
    public async Task Error_route_without_an_exception_redirects_to_the_home_page()
    {
        // ErrorController checks IExceptionHandlerPathFeature so that a user browsing straight to
        // /error does not get a fake 500.
        HttpResponseMessage response = await _noRedirectClient.GetAsync("/error");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.ToString(), Is.EqualTo("/"));
    }
}
