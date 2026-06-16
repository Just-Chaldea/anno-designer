# Release workflow

Releases are built and published by GitHub Actions. There are two kinds, each cut by pushing a tag to the fork.

## App release (`v*`)

The whole application, as a self-contained win-x64 build (users do not need the .NET runtime installed).

1. Bump the version in all five spots: `version.txt`, `AnnoDesigner/Constants.cs`, and the `AssemblyInfo.cs` files in `AnnoDesigner`, `AnnoDesigner.Core` and `PresetParser`.
2. Commit, then tag and push: `git tag -a v9.6 -m v9.6 && git push fork v9.6`.
3. `.github/workflows/release.yml` publishes the build, zips it, and creates the GitHub Release with `AnnoDesigner-v9.6.zip` attached.

The in-app update check reads `version.txt` from the default branch, so bumping `version.txt` is what tells existing installs an update is out.

## Presets release (`Presetsv*`)

Just the building data (`Presets/presets.json`), so data fixes can go out without a full app build. The in-app updater pulls it automatically.

1. Edit the data and bump the `Version` field in `Presets/presets.json`.
2. Tag with the matching version and push: `git tag Presetsv5.2 && git push fork Presetsv5.2`.
3. `.github/workflows/presets-release.yml` checks the tag matches that `Version`, then publishes `presets.json` as the release asset.

The tag version and the `Version` field have to match, or the app re-offers the update forever. We publish from this repo, not upstream, because our `presets.json` is merged with the Anno 117 data and an upstream legacy-only presets drop would overwrite it.

## Other preset assets the updater understands

Carried over from the original project, each release with a single matching asset. We only use the first one so far.

- `Presetsv*` to `presets.json` (building data)
- `Presetsv*` to `Presets.and.Icons.Update.*.zip` (presets plus new icons, for when a data change also needs icons shipped)
- `PresetsIconsv*` to `icons.json` (localized icon names)
- `PresetsColorsv*` to `colors.json` (predefined building colours)
