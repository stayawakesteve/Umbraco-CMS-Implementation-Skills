using Umbraco_CMS.Skills.TestHost.Shared;
using static Umbraco_CMS.Skills.TestHost.Shared.ContentPreconditions;

namespace Umbraco_CMS.Skills.TestHost.Blank;

/// <summary>
/// Content preconditions for SITE 2 (no starter kit). Its content comes from each example's own package
/// migration rather than from a starter kit, so a failure here means a migration didn't produce the
/// shape its example claims to need.
/// </summary>
[TestFixture]
public class BlankHostPreconditionsTests : ContentPreconditionsTestsBase
{
    protected override string Host => BlankHost;
    protected override HttpClient Client => BlankSiteFixture.Client;

    public static IEnumerable<ContentRequirement> Requirements() => DeclaredRequirements(BlankHost);

    [TestCaseSource(nameof(Requirements))]
    public Task Declared_content_requirement_is_met(ContentRequirement requirement) =>
        AssertRequirementAsync(requirement);
}
