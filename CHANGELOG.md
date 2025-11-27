# Changelog

This change log is to help track when new version of the nuget package are published. If the commit updates the package version the change log should be updated. Optionally update the [Unreleased](#unreleased) section of the change log when you PR to make this easier to do!

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

## [Unreleased]

### Changed

- Source generator now uses `MethodName` (Godot's generated StringName constants) instead of string literals or `nameof()` for better performance
  - Eliminates string-to-StringName conversion overhead on every command registration
  - Provides consistent type-safe method references for all methods (regardless of parameter count)
  - Resolves issue #13

## [0.0.1-beta-008] - 2025-05-31

- Added
  - [AutoComplete] attribute to allow for adding autocomplete sources to methods using [ConsoleCommand]
  - Solution (`Limbo.Console.Sharp.sln`) + new csproj's: `Limbo.Console.Generator.csproj` and `Limbo.Console.Abstractions.csproj` to support command generation from [ConsoleCommand] attributes


## [0.0.1-beta-007] - 2025-05-25

- Added
  - [ConsoleCommand] attribute to allow for easy creation of console commands
  - Solution (`Limbo.Console.Sharp.sln`) + new csproj's: `Limbo.Console.Generator.csproj` and `Limbo.Console.Abstractions.csproj` to support command generation from [ConsoleCommand] attributes

## [0.0.1-beta-006] - 2025-04-16

### Changed

- Updated LimboConsole class to be static for easier use

### Removed

- Removed all instuctions on wrapper initialization as initialization will happen on first use of the wrapper

## [0.0.1-beta-005] - 2025-04-12

### Changed

- Build pipeline changes

## [0.0.1-beta-004] - 2025-04-12

### Added

- `CHANGELOG.md` to the NuGet package metadata.

## [0.0.1-beta-003] - 2025-04-12

### Changed

- Updated project SDK from Godot.SDK to Microsoft.NET.Sdk for easier supporting of mutliple godot versions (this should also let the package truly support all godot 4.4.X versions)

## [0.0.1-beta-002] - 2025-04-12

### Changed

- README.md updated to better explain the project

## [0.0.1-beta-001] - 2025-04-11

### Added

- Initial release of `LimboConsole.Sharp`.
- Basic functionality for interacting with the `limbo_console` Godot plugin.
