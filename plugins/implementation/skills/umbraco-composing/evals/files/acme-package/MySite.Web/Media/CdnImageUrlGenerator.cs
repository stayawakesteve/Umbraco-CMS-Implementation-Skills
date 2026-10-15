using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;

namespace MySite.Web.Media;

/// <summary>Builds CDN image URLs. Reads the request's market, so it must be scoped.</summary>
public class CdnImageUrlGenerator : IImageUrlGenerator
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CdnImageUrlGenerator(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public IEnumerable<string> SupportedImageFileTypes => ["jpg", "jpeg", "png", "webp", "avif"];

    public string? GetImageUrl(ImageUrlGenerationOptions options)
    {
        if (options.ImageUrl is null)
        {
            return null;
        }

        string market = _httpContextAccessor.HttpContext?.Request.Headers["X-Market"].ToString() is { Length: > 0 } m
            ? m
            : "global";
        return $"https://cdn.mysite.example/{market}{options.ImageUrl}?w={options.Width}&h={options.Height}";
    }
}
