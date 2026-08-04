namespace Umbraco_CMS.Skills.TestHost.Shared;

/// <summary>
/// Fails loudly if two different Umbraco hosts are booted in the SAME process.
///
/// Umbraco keeps process-wide static state — notably StaticServiceProvider, which the
/// Umbraco.Extensions "friendly" extension methods resolve services from. A second host booted in a
/// process that already has one leaves skill code resolving from the wrong (or a disposed) provider.
/// Untrapped, the symptom is awful to diagnose: a fixture that passes when run alone and fails when
/// something else runs first, with an error that points anywhere but here.
///
/// Two hosts are meant to live in two test ASSEMBLIES, which `dotnet test` runs as separate
/// processes. This is the tripwire for the day that stops being true — someone merges the
/// assemblies, or a runner decides to batch them.
///
/// Uses AppDomain data rather than a static field on purpose: this file is compiled into BOTH test
/// assemblies, so a static would be per-assembly and could never see the other host. AppDomain data
/// is per-process, which is the scope the hazard actually lives at.
/// </summary>
public static class UmbracoHostSentinel
{
    private const string Key = "umbraco.skills.host";

    /// <summary>
    /// Records that <paramref name="hostName"/> owns this process. Throws if a different host got
    /// there first. Idempotent for the same host, so re-entry is harmless.
    /// </summary>
    public static void Claim(string hostName)
    {
        if (AppDomain.CurrentDomain.GetData(Key) is string other && other != hostName)
        {
            throw new InvalidOperationException(
                $"Two Umbraco hosts booted in one process ('{other}' then '{hostName}'). "
                + "Umbraco's StaticServiceProvider is process-wide static state, so the second "
                + "host leaves the first one's code resolving services from a dead provider. "
                + "Each host needs its own test assembly, run as its own process.");
        }

        AppDomain.CurrentDomain.SetData(Key, hostName);
    }
}
