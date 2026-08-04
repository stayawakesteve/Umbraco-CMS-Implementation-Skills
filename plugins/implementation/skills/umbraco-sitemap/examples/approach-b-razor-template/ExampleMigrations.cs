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

namespace Umbraco.Skills.Examples.SitemapApproachB;

/// <summary>
/// Loads and assembles this example's embedded manifests. Resource names are bare file names because
/// plugins/Directory.Build.props pins LogicalName when it embeds the generated assets.
/// </summary>
internal static class EmbeddedManifest
{
    public static string Text(string resourceName)
    {
        Assembly assembly = typeof(EmbeddedManifest).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. Available: "
                + string.Join(", ", assembly.GetManifestResourceNames()));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static XDocument Xml(string resourceName) => XDocument.Parse(Text(resourceName));

    /// <summary>
    /// Puts the template markup into the manifest's &lt;Design&gt; element for the given template alias.
    ///
    /// This splice is why the skill can ship its template as a real .cshtml file instead of a blob
    /// pasted inside XML: xmlSitemap.cshtml stays the single copy of the markup — the file users are
    /// told to copy — and it is the exact text installed and rendered here.
    /// </summary>
    public static XDocument WithTemplateDesign(this XDocument manifest, string templateAlias, string markup)
    {
        XElement design = manifest
            .Descendants("Template")
            .Where(t => (string?)t.Element("Alias") == templateAlias)
            .Select(t => t.Element("Design"))
            .FirstOrDefault(d => d is not null)
            ?? throw new InvalidOperationException(
                $"No <Template> with <Alias>{templateAlias}</Alias> and a <Design> element in the "
                + "manifest, so the template markup has nowhere to go.");

        design.ReplaceAll(new XCData(markup));
        return manifest;
    }

    /// <summary>
    /// Folds <paramref name="addition"/>'s sections into <paramref name="target"/> so both install as
    /// ONE manifest.
    ///
    /// Not a nicety. Umbraco's document type import topologically sorts each manifest's types by their
    /// compositions and allowed children with throwOnMissing enabled, and it only ever looks INSIDE the
    /// manifest being installed. The example's page type composes with xmlSiteMapSettings from the
    /// skill's manifest, so installing them as two packages fails with "Missing dependency ... with key
    /// xmlSiteMapSettings" even when that type already exists in the database.
    ///
    /// The two manifests stay separate FILES, which is what matters: the skill ships one, the example
    /// owns the other. They are only combined in memory, at the point of install.
    /// </summary>
    public static XDocument MergedWith(this XDocument target, XDocument addition)
    {
        foreach (XElement section in addition.Root!.Elements())
        {
            if (section.Name == "info")
            {
                continue; // package metadata, not content — merging it just duplicates the name
            }

            XElement? existing = target.Root!.Element(section.Name);
            if (existing is null)
            {
                target.Root.Add(section);
            }
            else
            {
                existing.Add(section.Elements());
            }
        }

        return target;
    }
}

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
        XDocument manifest = EmbeddedManifest.Xml("sitemap-package.xml")
            .WithTemplateDesign("xmlSiteMap", EmbeddedManifest.Text("xmlSitemap.cshtml"))
            .MergedWith(EmbeddedManifest.Xml("ExampleFixtureContent.xml"));

        _packagingService.InstallCompiledPackageData(manifest);
        return Task.CompletedTask;
    }
}
