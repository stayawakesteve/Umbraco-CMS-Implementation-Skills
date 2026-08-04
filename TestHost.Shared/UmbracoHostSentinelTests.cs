namespace Umbraco_CMS.Skills.TestHost.Shared;

/// <summary>
/// The sentinel exists to convert "two Umbraco hosts in one process" from a mystery failure into a
/// named one. A tripwire nobody has tested is not a tripwire, so this asserts it actually trips.
///
/// Compiled into BOTH test assemblies (the file is linked), which also proves the shared source
/// builds in each — cheap, and it runs in whichever host you happen to be debugging.
/// </summary>
[TestFixture]
public class UmbracoHostSentinelTests
{
    [Test]
    public void Claiming_a_second_host_in_the_same_process_throws()
    {
        // The assembly's [SetUpFixture] has already claimed this process for its own host, so any
        // other name is a genuine conflict.
        InvalidOperationException? error = Assert.Throws<InvalidOperationException>(
            () => UmbracoHostSentinel.Claim("some-other-host"));

        Assert.That(error!.Message, Does.Contain("StaticServiceProvider"),
            "the message must explain WHY two hosts can't share a process, or the next person "
            + "just deletes the guard");
    }

    [Test]
    public void Reclaiming_the_same_host_is_harmless()
    {
        // Idempotent: re-entry (a second OneTimeSetUp, a retried fixture) must not fail.
        string current = (string)AppDomain.CurrentDomain.GetData("umbraco.skills.host")!;

        Assert.DoesNotThrow(() => UmbracoHostSentinel.Claim(current));
    }
}
