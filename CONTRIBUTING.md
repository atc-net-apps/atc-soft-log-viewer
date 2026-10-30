# 🤝 Contributing

Thanks for helping with AtcSoft Log Viewer. This page covers how to build and test a change, how to write the commit, and how a release is made.

## 🛠️ Build and test

```powershell
dotnet build AtcSoft.Log.Viewer.slnx -c Release
dotnet test --solution AtcSoft.Log.Viewer.slnx -c Release --no-build
```

* ⚠️ **Build in Release before you push.** The analyzers (StyleCop, Sonar, ATC rules) are errors in Release, and CI builds Release.
* 🧪 **A bug fix starts with a test that fails.** Most behavior can be tested without UI: the collector and analyzer tests use real files and real file notifications (`test/Atc.LogFiles.Analyzer.Tests/FolderMonitoringTests.cs`).
* 🖥️ **UI tests** open real windows, so they skip unless `ATCSOFT_E2E=1`. See [UI tests](docs/e2e-tests.md) for how to run them; they also run every night in the **UI tests (nightly)** workflow.
* 📏 **Performance:** for a change on a hot path, run the benchmarks before and after: `dotnet run -c Release --project benchmark/Atc.LogFiles.Benchmarks -- --filter '*'`.

## ✍️ Commits

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/). The type decides where the change shows in the changelog and how the version moves:

| Type | Changelog section | Version |
|---|---|---|
| `feat:` | New features | minor (1.**1**.0) |
| `fix:` | Bug fixes | patch (1.0.**1**) |
| `fix(deps):` | Upgrades | patch |
| `perf:` | Performance improvements | patch |
| `refactor:` | Refactorings | patch |
| `docs:` | Documentation | patch |
| `revert:` | Reverts | patch |
| `chore:`, `ci:`, `test:` | not shown | none |

A `!` after the type (`feat!:`) or a `BREAKING CHANGE:` footer moves the major version. Describe what changed for the user, in plain words.

## 🚀 Releases

Releases are made by [release-please](https://github.com/googleapis/release-please) (`.github/workflows/release-please.yml`):

1. 📝 Every push to `main` updates an open pull request named **chore: release version x.y.z**. It holds the changelog and the new version in `Directory.Build.props` and `setup/Directory.Build.props`.
2. ✅ To release, merge that pull request.
3. 🏷️ release-please then tags `vx.y.z` and creates the GitHub release with the changelog.
4. 📦 The **Build MSI** job publishes the app on Windows, builds `AtcSoft.Log.Viewer.Installer.msi` with WiX, and the **Upload release assets** job attaches the MSI and its `.msi.sha256` checksum to the release.
5. 🔔 Installed apps see the new release at their next start: the update check reads `releases/latest` and offers the MSI by its name.

Don't rename the installer project or the MSI: the update check looks for `AtcSoft.Log.Viewer.Installer.msi` (a unit test guards this).

The workflow uses the `RELEASE_PLEASE_PAT` secret when it's there, so the release pull request triggers the build. Without it, it falls back to `GITHUB_TOKEN` and the release pull request gets no build check.