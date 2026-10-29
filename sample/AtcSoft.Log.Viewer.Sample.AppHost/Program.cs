// Starts the log simulator and ATC Log Viewer. The simulator writes to sample/output (git-ignored); open that folder in the viewer.
var builder = DistributedApplication.CreateBuilder(args);

var logFolder = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "output"));

builder
    .AddProject<Projects.Atc_LogFiles_Simulator>("log-simulator")
    .WithEnvironment("Simulator__Folder", logFolder)
    .WithExternalHttpEndpoints();

// The viewer targets Windows (WPF), so it is added by path instead of a project reference
builder.AddProject(
    "log-viewer",
    Path.Combine(builder.AppHostDirectory, "..", "..", "src", "AtcSoft.Log.Viewer.Wpf.App", "AtcSoft.Log.Viewer.Wpf.App.csproj"));

await builder.Build().RunAsync();