namespace Umbraco_CMS.Skills.TestHost;

/// <summary>
/// Boots the reference instance ONCE for the whole test assembly and shares it with every skill
/// fixture.
///
/// This is not just an optimisation. Umbraco keeps process-wide static state (notably
/// StaticServiceProvider, which the Umbraco.Extensions "friendly" extension methods resolve
/// services from), so a second host booted in the same process after a first one has been
/// disposed leaves skill code resolving services from a dead provider — the symptom is one
/// fixture passing alone and failing when another runs before it. One host per process avoids it.
///
/// A [SetUpFixture] in this namespace wraps every fixture in it: OneTimeSetUp runs before the
/// first, OneTimeTearDown after the last.
/// </summary>
[SetUpFixture]
public class ReferenceSiteFixture
{
    /// <summary>The shared instance. Use it to create extra clients (e.g. non-redirecting).</summary>
    public static ReferenceSiteFactory Factory { get; private set; } = null!;

    /// <summary>Default client: installed content ready, redirects followed.</summary>
    public static HttpClient Client { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task BootReferenceSite()
    {
        Factory = new ReferenceSiteFactory();
        Client = Factory.CreateClient(); // first call boots Umbraco + installs Clean into the test DB
        await Factory.WaitUntilContentInstalledAsync(Client); // don't race the install
    }

    [OneTimeTearDown]
    public void ShutDownReferenceSite()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }
}
