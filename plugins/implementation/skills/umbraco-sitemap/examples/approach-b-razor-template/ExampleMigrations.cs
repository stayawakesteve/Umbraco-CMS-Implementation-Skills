using System.Reflection;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Packaging;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Packaging;
using Umbraco.Skills.Examples.Fixtures;

namespace Umbraco.Skills.Examples.SitemapApproachB;

/// <summary>
/// Installs umbraco-sitemap Approach B into the blank reference host, standing in for the backoffice
/// steps a user would otherwise follow by hand.
/// </summary>
public class SitemapApproachBPlan : PackageMigrationPlan
{
    public SitemapApproachBPlan()
        : base("Umbraco Sitemap Approach B (example)")
    {
    }

    protected override void DefinePlan()
        => To<ImportSitemapApproachB>(new Guid("c7000000-0000-4000-8000-000000000001"));
}

/// <summary>
/// Installs the skill's schema (the XmlSiteMap type, its template, the settings composition) together
/// with this example's fixture content, as one manifest.
///
/// Calls IPackagingService.InstallCompiledPackageData directly rather than the inherited
/// `ImportPackage.FromXmlDataManifest(...).Do()` builder. That builder SILENTLY DOES NOTHING on
/// Umbraco 17.5.3: ImportPackageBuilderExpression.Execute() puts every install path inside
/// `if (EmbeddedResourceMigrationType != null)` with no else, so a manifest supplied as an XDocument is
/// validated, logged as "Package migration completed" in a few milliseconds, and then dropped.
///
/// FromEmbeddedResource&lt;T&gt;() does work, but it loads the manifest by naming convention and leaves
/// no seam to splice the template markup into — which is the whole point here.
/// </summary>
public class ImportSitemapApproachB : AsyncPackageMigrationBase
{
    private readonly IPackagingService _packagingService;

    public ImportSitemapApproachB(
        IPackagingService packagingService,
        IMediaService mediaService,
        MediaFileManager mediaFileManager,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IShortStringHelper shortStringHelper,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        IMigrationContext context,
        IOptions<PackageMigrationSettings> packageMigrationsSettings)
        : base(packagingService, mediaService, mediaFileManager, mediaUrlGenerators,
            shortStringHelper, contentTypeBaseServiceProvider, context, packageMigrationsSettings)
        => _packagingService = packagingService;

    protected override Task MigrateAsync()
    {
        XDocument manifest = EmbeddedManifest.Xml(typeof(ImportSitemapApproachB).Assembly, "sitemap-package.xml")
            .WithTemplateDesign("xmlSiteMap", EmbeddedManifest.Text(typeof(ImportSitemapApproachB).Assembly, "xmlSitemap.cshtml"))
            .MergedWith(EmbeddedManifest.Xml(typeof(ImportSitemapApproachB).Assembly, "ExampleFixtureContent.xml"));

        _packagingService.InstallCompiledPackageData(manifest);
        return Task.CompletedTask;
    }
}
