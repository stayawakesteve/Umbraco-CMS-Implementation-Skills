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

namespace Umbraco.Skills.Examples.CustomErrorPagesApproachB;

/// <summary>
/// Installs umbraco-custom-error-pages Approach B into the blank reference host, standing in for the
/// backoffice steps a user would otherwise follow by hand.
/// </summary>
public class CustomErrorPagesApproachBPlan : PackageMigrationPlan
{
    public CustomErrorPagesApproachBPlan()
        : base("Umbraco Custom Error Pages Approach B (example)")
    {
    }

    protected override void DefinePlan()
        => To<ImportErrorPagesApproachB>(new Guid("d1000000-0000-4000-8000-0000000000a1"));
}

/// <summary>
/// Imports the skill's schema — the ErrorPage404 Document Type and its template, with the markup from
/// errorPage404.cshtml spliced into the manifest so the file users copy is the file installed.
///
/// Calls IPackagingService.InstallCompiledPackageData directly rather than the inherited
/// `ImportPackage.FromXmlDataManifest(...).Do()` builder, which SILENTLY DOES NOTHING on Umbraco 17.5.3:
/// ImportPackageBuilderExpression.Execute() puts every install path inside
/// `if (EmbeddedResourceMigrationType != null)` with no else, so an XDocument manifest is validated,
/// logged as a completed migration, and dropped.
///
/// Only schema here. The 404 NODE is created in ExampleHostWiring, because a package manifest can only
/// import documents at the site root and site 2 keeps a single shared root.
/// </summary>
public class ImportErrorPagesApproachB : AsyncPackageMigrationBase
{
    private readonly IPackagingService _packagingService;

    public ImportErrorPagesApproachB(
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
        XDocument manifest = EmbeddedManifest.Xml(typeof(ImportErrorPagesApproachB).Assembly, "error-pages-package.xml")
            .WithTemplateDesign("errorPage404", EmbeddedManifest.Text(typeof(ImportErrorPagesApproachB).Assembly, "errorPage404.cshtml"));

        _packagingService.InstallCompiledPackageData(manifest);
        return Task.CompletedTask;
    }
}
