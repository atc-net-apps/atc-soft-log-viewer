// Starts the log simulator. It writes to sample/output (git-ignored); open that folder in ATC Log Viewer.
// The viewer is started on its own: Aspire starts desktop apps without a visible window.
var builder = DistributedApplication.CreateBuilder(args);

var logFolder = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "output"));

builder
    .AddProject<Projects.Atc_LogFiles_Simulator>("log-simulator")
    .WithEnvironment("Simulator__Folder", logFolder)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();