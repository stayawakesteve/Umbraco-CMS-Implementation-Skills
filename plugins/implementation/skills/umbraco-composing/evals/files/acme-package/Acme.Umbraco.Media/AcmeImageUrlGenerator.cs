using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;

namespace Acme.Umbraco.Media;

internal sealed class AcmeImageUrlGenerator : IImageUrlGenerator
{
    public IEnumerable<string> SupportedImageFileTypes => ["jpg", "jpeg", "png", "webp"];

    public string? GetImageUrl(ImageUrlGenerationOptions options)
        => options.ImageUrl is null ? null : $"https://img.acme.example{options.ImageUrl}";
}
