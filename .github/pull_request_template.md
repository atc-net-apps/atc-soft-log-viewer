## Description

<!-- What changes and why, if the linked issues don't say it. -->

## Checklist

- [ ] The PR targets `main` and links the related issues, if any.
- [ ] A test covers the change, and it failed before the fix.
- [ ] `dotnet build AtcSoft.Log.Viewer.slnx -c Release` is clean (the analyzers are errors in Release).
- [ ] All unit tests pass. For a UI change, the UI tests pass too (`ATCSOFT_E2E=1`, see `docs/e2e-tests.md`).
- [ ] The README and docs are updated where the behavior changed.
- [ ] Commit messages follow Conventional Commits (`fix:`, `feat:`, `feat!:` for a breaking change), see `CONTRIBUTING.md`.
