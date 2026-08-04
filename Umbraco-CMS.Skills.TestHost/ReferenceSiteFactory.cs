using Umbraco_CMS.Skills.TestHost.Shared;

namespace Umbraco_CMS.Skills.TestHost;

/// <summary>
/// Boots SITE 1 — the Clean reference instance, with every C#/DI-shaped skill example referenced.
///
/// Clean's content is imported by the unattended install on first boot, then the DB is reused.
/// See SkillSiteFactory for the shared boot and install-wait mechanics.
/// </summary>
public sealed class ReferenceSiteFactory : SkillSiteFactory<Program>
{
    protected override string DatabaseFileName => "Umbraco.Tests.sqlite.db";
}
