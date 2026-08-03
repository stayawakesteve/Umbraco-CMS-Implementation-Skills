using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Skills.Examples.Sitemap;

public class SitemapComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationHandler<ContentPublishedNotification, SitemapCacheInvalidator>();
        builder.AddNotificationHandler<ContentUnpublishedNotification, SitemapCacheInvalidator>();
        builder.AddNotificationHandler<ContentDeletedNotification, SitemapCacheInvalidator>();
    }
}
