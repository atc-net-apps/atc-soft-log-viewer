namespace Atc.LogFiles.Collector.Tests;

/// <summary>
/// The integration tests share a working directory and global logger state,
/// so they must not run in parallel.
/// </summary>
[CollectionDefinition(nameof(TestCollection), DisableParallelization = true)]
public sealed class TestCollection;