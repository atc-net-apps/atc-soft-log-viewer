# 🖥️ UI tests (end-to-end)

The project `test/AtcSoft.Log.Viewer.E2E.Tests` starts the real ATC Log Viewer and the sample Aspire AppHost, and drives them the way a user would:

- 🪟 **The viewer** through UI Automation ([FlaUI](https://github.com/FlaUI/FlaUI)). Only UI Automation patterns are used (invoke, toggle, select): no mouse moves and no keystrokes, so you can keep working while the tests run.
- 🌐 **The simulator page** in Edge, headless ([Playwright](https://playwright.dev/dotnet/)). It uses the Edge that is installed, so no browser download is needed.
- 🚀 **The sample AppHost** through [Aspire.Hosting.Testing](https://learn.microsoft.com/dotnet/aspire/testing/write-your-first-test), the same way F5 starts it.

## ▶️ Run them

The tests open windows on the desktop, so they skip unless `ATCSOFT_E2E=1`. CI runs every test project, and there they show as skipped.

```powershell
dotnet build -c Release
$env:ATCSOFT_E2E = 1
dotnet test --project test/AtcSoft.Log.Viewer.E2E.Tests -c Release --no-build
```

They take about a minute. Stop the sample AppHost in Visual Studio first: the tests start their own.

## 🧪 What they check

| Test | What it does | It fails when |
|---|---|---|
| 🧭 `Every_View_And_Dialog_Opens_Without_A_Crash` | Selects the ribbon's tabs and views, opens the highlights pane, and opens and closes About, Application settings and New profile | A step breaks, a crash dialog shows, or the app exits |
| 📂 `Follows_Appends_Rolls_Deletes_And_New_Files` | The simulator writes 1,000 entries, then appends, rolls by rename, deletes a rolled file and creates a file empty that gets its first line 3 s later | The status bar count is not exactly what was written (nothing lost, nothing twice) |
| 💥 `Window_Keeps_Answering_During_A_Burst` | Writes 50,000 entries at once while it reads the status bar | A read takes 2 s or more, or the count is wrong |
| 🌊 `Window_Keeps_Answering_At_5000_Entries_A_Second` | Writes 5,000 entries a second for 10 s with auto-scroll on, measures how fast the window answers, and reads how far the list is scrolled | A read takes 2 s or more, the count is wrong, or the list is not kept at the end |
| 📭 `Empty_Folder_Shows_A_Message_Not_A_Crash` | Opens a folder with no log files | The "no valid log files" message does not show, or the app crashes |
| 🎛️ `Simulator_Page_Controls_The_Writing` | Start, a burst, a roll and Stop on the simulator page, started by the AppHost without the viewer | The page does not change as expected, or it logs an error, a failed request or a page error |
| 🚀 `Viewer_Starts_From_The_AppHost_With_A_Window` | Starts the AppHost with the viewer, as F5 does | The viewer's window does not show |

Each live test opens its folder through a profile with no log format set. The viewer must find the format from the files. Before a fix, opening a folder this way crashed; without that fix, `Follows_Appends_Rolls_Deletes_And_New_Files` fails on the app's "Error" dialog.

## 🔒 What they never touch

- 💾 **Your data:** the viewer gets its own data folder (`ATCSOFT_LOGVIEWER_DATA_PATH`), so your profiles and recent files are not read or changed. The copy of data from earlier versions is skipped for such a folder.
- 📁 **`sample/output`:** the AppHost writes to a folder of the test run (`LogFolder=...`).
- ⚙️ **Your settings:** `Application__*` and `OTEL_*` variables of your own environment are not passed to the viewer.

Each run writes to `%TEMP%\AtcSoft.Log.Viewer.E2E\<time>`: the log folders, the viewer's data and output, and, when a step fails, a dump of the window's UI Automation tree. The folder stays after the run, so you can see what happened.

## 📏 Numbers from a run

These come from the test output: a Release build on a developer machine, reading the status bar through UI Automation every 100 ms.

| Load | Median read | Slowest read |
|---|---|---|
| Idle | 69 ms | |
| 5,000 entries a second, auto-scroll on | 234 ms | 476 ms |
| The same before the auto-scroll fix | | no answer for more than 10 s |

The steady-rate test found the freeze. Live entries are added to the list one by one, so the selection and scroll position are kept, and the auto-scroll behavior scrolled after each one. It now scrolls once when the window has drawn the new entries (`AutoScrollToEndBehavior`), and the test checks that the list still stays at the end (100 % during and after the flow).

## ✍️ Writing a new test

- 🔎 Find elements by their text or an automation id (`AutomationProperties.AutomationId`). The status bar counts have the ids `CollectedCount` and `ShownCount`, and the list has `LogEntries`.
- 🌳 Call `app.DumpTree("label")` to see the UI Automation tree of every window. The Application menu, for example, is an unnamed `Menu` with the class `Backstage`.
- 🧱 Use `SmokeSteps` for a walk through many dialogs, so one run reports every step that breaks.
- 📋 The list is virtualized: UI Automation sees only the rows on the screen. Read counts from the status bar, not by counting rows.