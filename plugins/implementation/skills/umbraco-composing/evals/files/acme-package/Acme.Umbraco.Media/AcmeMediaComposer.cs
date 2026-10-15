using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Media;
using Umbraco.Extensions;

namespace Acme.Umbraco.Media;

internal sealed class AcmeMediaComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Services.AddUnique<IImageUrlGenerator, AcmeImageUrlGenerator>(ServiceLifetime.Scoped);
}
