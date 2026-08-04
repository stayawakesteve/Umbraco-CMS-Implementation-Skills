using Umbraco_CMS.Skills.TestHost.Shared;

namespace Umbraco_CMS.Skills.TestHost.Blank;

/// <summary>
/// Boots SITE 2 — the Clean-less reference instance, with every content/config-shaped skill example
/// referenced.
///
/// Unlike site 1, nothing here arrives with the install: the site starts genuinely empty, and each
/// referenced example's package migration seeds the Document Types, templates and content its
/// approach needs during boot. That is the point of the host — the only content shape present is
/// the one the skill's own guidance describes, so an assertion can't accidentally be satisfied by a
/// starter kit's implementation of the same feature.
/// </summary>
public sealed class BlankSiteFactory : SkillSiteFactory<BlankProgram>
{
    protected override string DatabaseFileName => "Umbraco.Blank.Tests.sqlite.db";
}
