// Starts the log simulator and ATC Log Viewer. The simulator writes to sample/output (git-ignored); open that folder in the viewer.
// Settings (for the end-to-end tests): LogFolder moves the folder, StartViewer=false leaves the viewer out,
// ViewerDataPath gives the viewer a data folder of its own (profiles, recent files).
var builder = DistributedApplication.CreateBuilder(args);

var logFolder = Path.GetFullPath(
    builder.Configuration["LogFolder"] is { Length: > 0 } folder
        ? folder
        : Path.Combine(builder.AppHostDirectory, "..", "output"));

builder
    .AddProject<Projects.Atc_LogFiles_Simulator>("log-simulator")
    .WithEnvironment("Simulator__Folder", logFolder)
    .WithExternalHttpEndpoints();

if (!string.Equals(builder.Configuration["StartViewer"], "false", StringComparison.OrdinalIgnoreCase))
{
    // The viewer targets Windows (WPF), so it is added by path instead of a project reference
    var viewer = builder.AddProject(
        "log-viewer",
        Path.Combine(builder.AppHostDirectory, "..", "..", "src", "AtcSoft.Log.Viewer.Wpf.App", "AtcSoft.Log.Viewer.Wpf.App.csproj"));

    if (builder.Configuration["ViewerDataPath"] is { Length: > 0 } viewerDataPath)
    {
        viewer.WithEnvironment("ATCSOFT_LOGVIEWER_DATA_PATH", viewerDataPath);
    }
}

await builder.Build().RunAsync();