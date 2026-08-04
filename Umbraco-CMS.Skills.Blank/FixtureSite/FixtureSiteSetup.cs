using System.Reflection;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Packaging;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Packaging;
using Umbraco.Skills.Examples.Fixtures;

namespace Umbraco_CMS.Skills.Blank.FixtureSite;

/// <summary>
/// Installs site 2's BASELINE content — the single root node every content-shaped example hangs its
/// subtree from. This is the host's job, not any example's: it's the counterpart to what the Clean
/// starter kit provides on site 1, and putting it here is what stops two examples each creating a root
/// and changing every URL on the site.
/// </summary>
public class FixtureSitePlan : PackageMigrationPlan
{
    public FixtureSitePlan()
        : base("Site 2 fixture site (baseline)")
    {
    }

    protected override void DefinePlan()
        => To<ImportFixtureSite>(new Guid("a0000000-0000-4000-8000-00000000f010"));
}

/// <summary>
/// Imports the baseline Document Type, template and root node.
///
/// Calls IPackagingService.InstallCompiledPackageData directly rather than the inherited
/// `ImportPackage.FromXmlDataManifest(...).Do()`: that builder silently does nothing on Umbraco 17.5.3,
/// because ImportPackageBuilderExpression.Execute() puts every install path inside
/// `if (EmbeddedResourceMigrationType != null)` with no else. It logs a completed migration in a few
/// milliseconds and creates nothing.
/// </summary>
public class ImportFixtureSite : AsyncPackageMigrationBase
{
    private readonly IPackagingService _packagingService;

    public ImportFixtureSite(
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
        Assembly assembly = typeof(ImportFixtureSite).Assembly;
        using Stream stream = assembly.GetManifestResourceStream("fixture-site-package.xml")
            ?? throw new InvalidOperationException(
                "fixture-site-package.xml is not embedded. Available: "
                + string.Join(", ", assembly.GetManifestResourceNames()));
        using var reader = new StreamReader(stream);

        _packagingService.InstallCompiledPackageData(XDocument.Parse(reader.ReadToEnd()));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Publishes the baseline root once the migrations have run, so the site is routable even with no
/// examples loaded. Examples publish their own subtrees the same way; both are idempotent, which is what
/// keeps them independent of each other's ordering.
/// </summary>
public class FixtureSiteInitializer : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IContentService _contentService;
    private readonly IContentPublishingService _publishingService;
    private readonly IDocumentUrlService _documentUrlService;
    private readonly IDatabaseCacheRebuilder _cacheRebuilder;

    public FixtureSiteInitializer(
        IContentService contentService,
        IContentPublishingService publishingService,
        IDocumentUrlService documentUrlService,
        IDatabaseCacheRebuilder cacheRebuilder)
    {
        _contentService = contentService;
        _publishingService = publishingService;
        _documentUrlService = documentUrlService;
        _cacheRebuilder = cacheRebuilder;
    }

    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        IContent? root = Umbraco.Skills.Examples.Fixtures.FixtureSite.FindRoot(_contentService);
        if (root is null)
        {
            throw new InvalidOperationException(
                "Site 2 has no root node, so the fixture-site migration did not run. Every "
                + "content-shaped example depends on it.");
        }

        await Umbraco.Skills.Examples.Fixtures.FixtureSite.PublishAndRefreshAsync(
            _publishingService, _documentUrlService, _cacheRebuilder, root);
    }
}

/// <summary>Registers the initializer. Picked up by AddComposers() in Program.cs.</summary>
public class FixtureSiteComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, FixtureSiteInitializer>();
}
