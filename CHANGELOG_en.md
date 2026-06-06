**🌐[ [中文](CHANGELOG.md) | English ]**

# 🧾 Changelog

All notable changes to this project will be recorded in this file.

Versioning rule: major.minor.patch. The major version is used for larger feature-complete milestones, the minor version is generally updated when a new Steam Workshop version is published, and the patch version is updated after each code-related commit, starting from 0.

## [1.3.2] - 2026-6-6
### Fixed
- Fixed an issue where registering controller events after a game version update could invalidate controller layouts.

## [1.3.0] - 2026-6-5
### Added
- Added pause menu entry extension APIs, allowing child mods to add button entries to the in-run pause menu through `[PauseMenuButton]` or manual registration.
- Pause menu entries support stable ordering, localization fallback, click context, exception isolation, and keyboard/controller focus-chain integration.

## [1.2.0] - 2026-5-26
### Fixed
- Fixed Attribute scanning being interrupted on Android and other restricted runtimes when dynamic reflection accessors fail to initialize, which could prevent `[Config]`, `[UIButton]`, `[JmcHotkey]`, and `[UIHotkey]` from registering.
- Reflection accessors now fall back to standard reflection calls when dynamic IL or expression delegates are unavailable, keeping config and hotkey systems usable across platforms.

## [1.1.0] - 2026-5-8
### Added
- Added the JmcModLib black-and-gold badge avatar.
- Official release.

## [1.0.105] - 2026-5-7
### Added
Initial version release.
Added English versions of the README, CHANGELOG, API Reference, and Quick Start docs, with language switch links for each document pair.
