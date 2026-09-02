---
name: presentationtimer-release
description: Publish PresentationTimer GitHub Releases. Use when Codex needs to recommend a release version, preview a release, build the Windows installer locally, create and push a v*.*.* tag to trigger GitHub Actions release assets, or explain/fix the PresentationTimer release workflow.
---

# PresentationTimer Release

## Overview

Use this skill to release PresentationTimer from the repository root by calling `scripts/release.ps1`. The script is the source of truth for local GitHub Release automation; do not reimplement tag creation, test execution, or pushing by hand unless the script is missing or broken.

`scripts/build-installer.ps1` builds the Inno Setup installer locally from the unpackaged self-contained publish output, for preview or when CI is not an option. It never creates tags or publishes anything.

GitHub Release publication happens in `.github/workflows/release.yml`. Pushing a `v*.*.*` tag triggers the workflow: tests, publish, installer build, then the GitHub Release is created or updated with the workflow's `GITHUB_TOKEN`. Do not attempt to create the release from local `gh` credentials; the local `gh` logins in this environment cannot see the private `hongjiapeng/PresentationTimer` repository.

## Release State Preflight

1. Inspect local and remote release state before interpreting a generic request such as "publish":
   - `git status --short --branch`
   - `git tag --list "v*" --sort=-version:refname`
   - `git ls-remote --tags origin` (the remote is `git@github.com:hongjiapeng/PresentationTimer.git`; `gh release list` may 404 from local credentials, so use `git ls-remote` for tag checks)
2. If the user also wants to verify the published release or the Actions run, ask the repository owner to confirm on the GitHub Actions page or the Releases page, because local credentials cannot read the private repository's CI or release state.
3. Use precise state language:
   - A pushed tag is not yet a completed GitHub Release. The release is complete only after the GitHub Actions run succeeds and both assets exist.
   - A locally built installer under `dist/` is a preview artifact, not a release asset.

## Version Choice

- Prefer SemVer tags in the form `vMAJOR.MINOR.PATCH`.
- Accept user input as either `0.1.0` or `v0.1.0`; the script normalizes it to `v0.1.0`.
- For the first public release of PresentationTimer, recommend `v0.1.0` unless existing tags or user intent suggest a different version.
- Use `v0.1.1` for bug-fix-only follow-ups, `v0.2.0` for meaningful feature additions before stability, and `v1.0.0` only when the project is ready to be presented as stable.
- Avoid four-part tags such as `v0.1.0.0` for GitHub Releases. Four-part versions are appropriate for Windows/.NET file versions, not release tags.
- Keep the default version metadata aligned with the new release in `Directory.Build.props`, the Inno Setup default, the local installer script default, and README build examples. The workflow's `-p:Version` override does not replace source-level version hygiene.

## GitHub Release Workflow

1. Confirm the repository context:
   - Run `git remote -v` and verify the remote points at `hongjiapeng/PresentationTimer`.
   - Run `git status --short --branch` and treat uncommitted changes as a release blocker unless the user explicitly wants to release from a dirty tree.
   - Check existing tags with `git tag --list "v*"` and `git ls-remote --tags origin` when choosing or validating a version.
   - Inspect `git log --graph --oneline --decorate --all` and verify the release commit is a descendant of the previous release tag. Merge the intended feature branch into `main` before tagging; do not create a divergent release history.
2. If the user wants a preview, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\release.ps1 0.1.0 -DryRun -AllowDirty -SkipTests
```

Use `-AllowDirty` only for dry runs while preparing local changes.

3. If the user wants to preview the installer locally, Inno Setup 6 must be installed, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Version 0.1.0
```

This produces `dist\PresentationTimer-<version>-win-x64-Setup.exe`.

4. For a real release, first make sure all intended changes are committed and pushed. Then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\release.ps1 0.1.0
```

5. After the tag push succeeds, GitHub Actions creates the release assets from `.github/workflows/release.yml`: tests run on `windows-latest`, the app is published, the Inno Setup installer and the portable `presentationtimer-<tag>-win-x64.zip` are built, and the GitHub Release is created or updated.

## Guardrails

- Do not pass `-AllowDirty` for a real release unless the user explicitly requests it and understands the risk.
- Do not pass `-SkipTests` for a real release unless the user explicitly requests it.
- Do not manually create lightweight tags for normal releases; use the script so annotated tags are created consistently.
- If `scripts/release.ps1` is missing, stop and explain that the skill depends on that script instead of inventing a parallel process.
- If the local remote points at a different repository than `hongjiapeng/PresentationTimer`, call out the repository mismatch before releasing.
- Do not create the GitHub Release with local `gh` credentials; only the workflow's `GITHUB_TOKEN` has reliable access to this private repository.
