namespace AtcSoft.Log.Viewer.E2E;

/// <summary>
/// One desktop: the UI tests run one at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public static class E2ETestGroup
{
    public const string Name = "E2E UI tests";
}