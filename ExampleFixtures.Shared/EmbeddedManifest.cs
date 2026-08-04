using System.Reflection;
using System.Xml.Linq;

namespace Umbraco.Skills.Examples.Fixtures;

/// <summary>
/// Loads and assembles an example's embedded manifests. Resource names are bare file names because
/// plugins/Directory.Build.props pins LogicalName when it embeds the generated assets.
///
/// The CALLER supplies its own assembly. This helper cannot use its own: the manifests are embedded in
/// each example, not here. (While this file was linked into every example rather than referenced, each
/// consumer got a private copy whose `typeof(EmbeddedManifest).Assembly` happened to be the right one —
/// which is precisely the fragile type duplication the CS0436 warning was pointing at.)
/// </summary>
public static class EmbeddedManifest
{
    public static string Text(Assembly assembly, string resourceName)
    {
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. Available: "
                + string.Join(", ", assembly.GetManifestResourceNames()));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static XDocument Xml(Assembly assembly, string resourceName) =>
        XDocument.Parse(Text(assembly, resourceName));

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
