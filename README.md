[![License](https://img.shields.io/github/license/Just-Chaldea/anno-designer)](https://github.com/Just-Chaldea/anno-designer/blob/main/LICENSE) [![version](https://img.shields.io/badge/latest--version-9.5-blue)](https://github.com/Just-Chaldea/anno-designer/releases/tag/v9.5) [![CI](https://github.com/Just-Chaldea/anno-designer/actions/workflows/ci.yml/badge.svg)](https://github.com/Just-Chaldea/anno-designer/actions/workflows/ci.yml) [![Discord](https://img.shields.io/discord/571011757317947406?label=help%2Fdiscord)](https://discord.gg/JJpHWRB)

# Anno Designer

A building layout designer for Ubisoft's Anno-series.

This is an actively maintained fork of the community [AnnoDesigner/anno-designer](https://github.com/AnnoDesigner/anno-designer) project, originally created by [JcBernack](https://github.com/JcBernack/anno-designer). It adds **Anno 117: Pax Romana** support and an in-app savegame importer, and brings the app up to .NET 9.

## What's new in this fork

- **Anno 117 (Pax Romana) support**: buildings, icons, population tiers, and the new road influence reach mechanic.
- **Savegame import**: load a `.a8s` save straight into the designer with **File > Import Anno 117 Savegame**. Islands, fields, roads, aqueducts, drainage channels and diagonal placements are rendered where they sit in your game.
- **.NET 9**: the releases are self-contained, so there is no runtime to install.

The app still supports all of the previous games as well.

## Latest release

### [Anno Designer 9.5](https://github.com/Just-Chaldea/anno-designer/releases/tag/v9.5)

Download the zip, extract it anywhere, and run `AnnoDesigner.exe`. The building presets and icons are bundled, so there is no separate presets download.

## Discord

Keep up to date with the latest developments. If you have ideas or questions, or run into an error with the designer, the Discord is a good place to ask.

<https://discord.gg/JJpHWRB>

## Summary

The **Anno Designer** is a standalone Windows application for creating and exporting building layouts. It uses a drag and drop system and is intuitive and easy to use.
**Supported Anno versions: 1404, 2070, 2205, 1800, and 117 (Pax Romana).**

## How to use

**Download the latest release [here](https://github.com/Just-Chaldea/anno-designer/releases/tag/v9.5)**, extract the zip, and run `AnnoDesigner.exe` to start designing layouts.

To bring an existing Anno 117 city into the designer, use **File > Import Anno 117 Savegame** and pick the island you want.

Anno Designer can also be started from the command line for advanced usages. See [this documentation](doc/CommandLineParameters.md) for more information and examples.

## Technology

Written in C# (.NET 9) and uses WPF. Releases are self-contained win-x64 builds, so the .NET runtime does not need to be installed separately. Builds and releases are produced by GitHub Actions.

## Game data and icons

The building presets and icons are extracted from the game files. For the legacy games this uses the [RDA explorer](https://github.com/lysannschlegel/RDAExplorer) and a custom script written by Peter Hozak (see the development pages on the [Anno 2070 wiki](http://anno2070.wikia.com/wiki/Development_Pages)). [StingMcRay](https://github.com/StingMcRay) extracted a lot of the Anno 2205 and Anno 1800 icons. The Anno 117 building data is built with the community [anno-mods/asset-extractor](https://github.com/anno-mods/asset-extractor) plus a converter in this repo.

Included in this repo is a modified version of the PresetParser, which supports extracting data from the different Anno versions. It is not required to run the app, and is not included in any release.

## Acknowledgements

This fork builds directly on the work of others:

- [JcBernack](https://github.com/JcBernack/anno-designer) created the original Anno Designer, and the [AnnoDesigner](https://github.com/AnnoDesigner/anno-designer) community carried it forward.
- The Anno 117 savegame importer and the initial 117 preset work come from [oliversaggau](https://github.com/oliversaggau/anno-designer) ([#4](https://github.com/oliversaggau/anno-designer/pull/4) and [#3](https://github.com/oliversaggau/anno-designer/pull/3)).
- The diagonal building support is based on [taubenangriff](https://github.com/taubenangriff)'s case study ([AnnoDesigner#456](https://github.com/AnnoDesigner/anno-designer/pull/456)).

## License

[MIT](https://github.com/Just-Chaldea/anno-designer/blob/main/LICENSE)
