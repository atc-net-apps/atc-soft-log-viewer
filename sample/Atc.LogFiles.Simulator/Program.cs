var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection(SimulatorOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LogSimulator>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/state", (LogSimulator simulator) => simulator.GetState());

api.MapPut("/settings", (SimulatorSettings settings, LogSimulator simulator) =>
{
    simulator.UpdateSettings(settings);
    return simulator.GetState();
});

api.MapPost("/start", (LogSimulator simulator) =>
{
    simulator.Start();
    return simulator.GetState();
});

api.MapPost("/stop", (LogSimulator simulator) =>
{
    simulator.Stop();
    return simulator.GetState();
});

api.MapPost("/roll", (LogSimulator simulator) =>
{
    simulator.RollNow();
    return simulator.GetState();
});

api.MapPost("/burst/{count:int}", (int count, LogSimulator simulator) =>
{
    simulator.Burst(count);
    return simulator.GetState();
});

api.MapPost("/empty-file", (LogSimulator simulator) =>
{
    simulator.CreateEmptyFile();
    return simulator.GetState();
});

api.MapPost("/files/delete-all", (LogSimulator simulator) =>
{
    simulator.DeleteAllFiles();
    return simulator.GetState();
});

api.MapPost("/files/{name}/delete", (string name, LogSimulator simulator) =>
{
    simulator.DeleteFile(name);
    return simulator.GetState();
});

await app.RunAsync();