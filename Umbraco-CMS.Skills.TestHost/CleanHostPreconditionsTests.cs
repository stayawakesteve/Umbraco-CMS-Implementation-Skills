using Umbraco_CMS.Skills.TestHost.Shared;
using static Umbraco_CMS.Skills.TestHost.Shared.ContentPreconditions;

namespace Umbraco_CMS.Skills.TestHost;

/// <summary>
/// Content preconditions for SITE 1 (Clean). All logic lives in the shared base; this fixture only
/// binds it to this assembly's host and client. The [TestCaseSource] method has to be here because
/// NUnit requires a static source, and only this assembly knows which host to filter for.
/// </summary>
[TestFixture]
public class CleanHostPreconditionsTests : ContentPreconditionsTestsBase
{
    protected override string Host => CleanHost;
    protected override HttpClient Client => ReferenceSiteFixture.Client;

    public static IEnumerable<ContentRequirement> Requirements() => DeclaredRequirements(CleanHost);

    [TestCaseSource(nameof(Requirements))]
    public Task Declared_content_requirement_is_met(ContentRequirement requirement) =>
        AssertRequirementAsync(requirement);
}
