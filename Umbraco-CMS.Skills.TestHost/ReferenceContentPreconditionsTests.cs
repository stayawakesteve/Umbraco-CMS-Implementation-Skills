using System.Net;
using System.Text.Json;

namespace Umbraco_CMS.Skills.TestHost;

/// <summary>
/// Asserts the CONTENT SHAPE the skill examples navigate to, independently of the skills themselves.
///
/// The examples resolve nodes structurally — "first child of the site root whose Document Type
/// alias is X" — so they only prove something while the starter kit actually has that shape.
/// Without these checks, bumping the Clean version and losing or renaming a node surfaces as (for
/// instance) CustomErrorPagesTests failing on "expected body to contain 'Page not found'": true,
/// but pointing at the skill rather than at the content. A failure here names the real cause.
///
/// Requirements are DECLARED BY EACH SKILL, in its example/.generate.json `requires` block, and
/// discovered here — so adding a skill to the gate needs no new test code, and nothing in this
/// file mentions any particular skill. A `&lt;Placeholder&gt;` entry is resolved through the same
/// manifest's `placeholders` map, keeping each alias in exactly one place.
///
/// Read through the Delivery API rather than the database: it's a public, versioned contract, it
/// needs no authentication (so nothing here depends on the MCP or its API user), it reports each
/// node's contentType as its ALIAS, and it serves only PUBLISHED content — the same visibility the
/// skills' own lookups have.
/// </summary>
[TestFixture]
public class ReferenceContentPreconditionsTests
{
    /// <summary>A content precondition one skill's example declared.</summary>
    public record ContentRequirement(string Skill, string Kind, string Value)
    {
        public override string ToString() => $"{Skill} needs {Kind} '{Value}'";
    }

    private static HttpClient Client => ReferenceSiteFixture.Client;

    /// <summary>
    /// Walks up from the test assembly to the repo root — the tests run from bin/, and the
    /// manifests live next to the skills, so neither path can be hardcoded relative to the other.
    /// </summary>
    private static DirectoryInfo RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Umbraco-CMS.Skills.sln")))
        {
            dir = dir.Parent;
        }

        Assert.That(dir, Is.Not.Null, "could not locate the repo root (no Umbraco-CMS.Skills.sln above the test assembly)");
        return dir!;
    }

    /// <summary>
    /// Every <c>&lt;skill&gt;/examples/&lt;approach&gt;/.generate.json</c>. Build output is skipped:
    /// the SDK copies the manifest into bin/, and those copies would otherwise look like extra
    /// examples declaring the same requirements.
    /// </summary>
    private static List<FileInfo> ExampleManifests() =>
        new DirectoryInfo(Path.Combine(RepoRoot().FullName, "plugins"))
            .GetFiles(".generate.json", SearchOption.AllDirectories)
            .Where(f => f.Directory?.Parent?.Name == "examples")
            .OrderBy(f => f.FullName)
            .ToList();

    /// <summary>
    /// Every requirement declared across all example manifests. NUnit turns each into its own test
    /// case, so a broken precondition is reported against the skill that declared it.
    /// </summary>
    public static IEnumerable<ContentRequirement> DeclaredRequirements()
    {
        foreach (FileInfo manifest in ExampleManifests())
        {
            // "<skill>/<approach>" — used only for reporting, so a failure names which example.
            string skill = manifest.Directory?.Parent?.Parent?.Name is string s
                ? $"{s}/{manifest.Directory!.Name}"
                : manifest.FullName;

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifest.FullName));
            if (!doc.RootElement.TryGetProperty("requires", out JsonElement requires))
            {
                continue; // declaring nothing is fine — plenty of skills need no particular node
            }

            Dictionary<string, string> placeholders =
                doc.RootElement.TryGetProperty("placeholders", out JsonElement p)
                    ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString() ?? string.Empty)
                    : new Dictionary<string, string>();

            foreach (JsonProperty kind in requires.EnumerateObject())
            {
                foreach (JsonElement entry in kind.Value.EnumerateArray())
                {
                    string declared = entry.GetString() ?? string.Empty;

                    // "<ErrorPageAlias>" means "whatever that placeholder resolves to", so the
                    // literal value is never written twice. An unresolvable token is a manifest
                    // bug, and silently testing the token itself would hide it.
                    string value = placeholders.TryGetValue(declared, out string? resolved)
                        ? resolved
                        : declared;

                    Assert.That(value, Does.Not.StartWith("<"),
                        $"{skill}: requires '{declared}' but no such key exists in this manifest's placeholders");

                    yield return new ContentRequirement(skill, kind.Name, value);
                }
            }
        }
    }

    /// <summary>Direct children of the site root, as (name, contentTypeAlias) pairs.</summary>
    private static async Task<List<(string Name, string ContentType)>> GetRootChildrenAsync()
    {
        HttpResponseMessage response =
            await Client.GetAsync("/umbraco/delivery/api/v2/content?fetch=children:/&take=100");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "the Delivery API must answer — it's how these preconditions are read");

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => (
                Name: item.GetProperty("name").GetString() ?? string.Empty,
                ContentType: item.GetProperty("contentType").GetString() ?? string.Empty))
            .ToList();
    }

    [Test]
    public void Example_manifests_are_discoverable()
    {
        // Guards the discovery above: if the glob broke, the requirement cases below would silently
        // become an empty set and gate nothing at all.
        Assert.That(ExampleManifests(), Is.Not.Empty,
            "expected at least one plugins/**/example/.generate.json — discovery is broken");
    }

    [Test]
    public async Task Site_root_has_published_children()
    {
        List<(string Name, string ContentType)> children = await GetRootChildrenAsync();

        // Applies to every skill that renders or enumerates content: the sitemap example has
        // nothing to emit without these, and an empty <urlset> looks like a broken controller.
        Assert.That(children, Is.Not.Empty,
            "the starter kit's published pages are what the content-driven examples work against");
    }

    [TestCaseSource(nameof(DeclaredRequirements))]
    public async Task Declared_content_requirement_is_met(ContentRequirement requirement)
    {
        List<(string Name, string ContentType)> children = await GetRootChildrenAsync();
        string available = string.Join(", ", children.Select(c => $"{c.Name} [{c.ContentType}]"));

        switch (requirement.Kind)
        {
            case "documentTypeAliasAtRoot":
                // Assets navigate root -> FirstChildOfType(alias), so the node must be a DIRECT
                // child: one nested a level deeper resolves to null with no error.
                Assert.That(children.Select(c => c.ContentType), Has.Member(requirement.Value),
                    $"{requirement.Skill}: no published DIRECT child of the site root has Document "
                    + $"Type alias '{requirement.Value}', so its example cannot resolve that node. "
                    + $"Available: {available}");
                break;

            default:
                // Better to fail than to skip: an unrecognised kind means a manifest declared a
                // precondition this fixture doesn't check, which would otherwise pass vacuously.
                Assert.Fail(
                    $"{requirement.Skill}: unknown requirement kind '{requirement.Kind}'. Add a case "
                    + $"for it here, or correct the manifest.");
                break;
        }
    }
}
