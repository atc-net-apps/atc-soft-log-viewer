# Log simulator

Watch ATC Log Viewer follow log files while they are written, rolled and deleted.

| Project | What |
|---|---|
| `AtcSoft.Log.Viewer.Sample.AppHost` | Aspire AppHost: starts the simulator and ATC Log Viewer, and shows both in the Aspire dashboard |
| `Atc.LogFiles.Simulator` | A small web app that writes log files to `sample/output` and has a page to control it |

## Run it

```powershell
dotnet run --project sample/AtcSoft.Log.Viewer.Sample.AppHost
```

1. The Aspire dashboard opens. Click the `log-simulator` endpoint to open the simulator page.
2. ATC Log Viewer opens as `log-viewer`. The first start can take a while, because Aspire builds it with `dotnet run`.
3. In the viewer, open the folder shown at the top of the simulator page (**Copy path**), which is `sample/output`.
4. Press **Start** on the simulator page, then try the buttons and watch the viewer.

To run the simulator alone: `dotnet run --project sample/Atc.LogFiles.Simulator`. It then writes to `%TEMP%\atc-log-simulator`, or to the folder in the `Simulator__Folder` environment variable.

## What you can try

| On the page | What it does | What it tests in the viewer |
|---|---|---|
| Entries per second, files, errors %, stack traces % | Writes entries to one file per service (`orders.log`, `payments.log`, …) | Live tail, multi-line entries, many files at once |
| Format | Serilog, NLog or log4net default layout | The parser for each format |
| Roll: **Rename** | `orders.log` → `orders.log.1`, older ones shift up, a new `orders.log` starts (log4net) | A tailed file that is renamed; its last lines must not be lost |
| Roll: **New file** | New entries go to `orders-<time>.log` (Serilog and NLog archives) | Picking up new files in the folder |
| Roll: **Copy and truncate** | `orders.log` is copied to `orders.log.<time>` and emptied (logrotate) | A tailed file that is truncated |
| Roll at size | Rolls a file automatically when it reaches the size | Rolling under load |
| Burst 1,000 / 10,000 / 100,000 | Writes many entries at once | That the viewer stays responsive |
| New empty file | Creates `late-1.log` empty and writes its first line 3 seconds later | A new file that can't be read as a log yet |
| Delete | Deletes a file, or all files | A tailed file that disappears, and comes back |

File actions that fail, for example creating a file the viewer still has open, show in **What happened** on the page.
