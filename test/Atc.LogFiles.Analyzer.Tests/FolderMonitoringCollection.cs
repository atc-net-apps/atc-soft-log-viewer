namespace Atc.LogFiles.Analyzer.Tests;

/// <summary>
/// The folder monitoring tests use real file notifications and timing, so they don't run in parallel.
/// </summary>
[CollectionDefinition(nameof(FolderMonitoringCollection), DisableParallelization = true)]
public sealed class FolderMonitoringCollection;