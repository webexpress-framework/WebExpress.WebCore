namespace WebExpress.WebCore.Test.Fixture
{
    /// <summary>
    /// Defines a collection of tests that should not be run in parallel.
    /// </summary>
    /// <remarks>
    /// Only tests that change or depend on process-wide state belong here: the process-wide
    /// component hub, a running server, shared files and stores, the log, the id counter or the
    /// default culture - and tests whose outcome rests on short delays or timeouts, which a busy
    /// machine stretches. Every other test binds its own hub and runs in parallel.
    /// </remarks>
    [CollectionDefinition("NonParallelTests", DisableParallelization = true)]
    public class NonParallelTestsCollection : ICollectionFixture<UnitTestFixture>
    {

    }
}
