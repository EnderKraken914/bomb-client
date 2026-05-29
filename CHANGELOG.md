# Changelog

## 2.0.0 - 2026-05-29

### New Features

- Added Bomb Client 2.0 Core module architecture with module id, display name, description, category, enabled state, settings metadata, and saved config.
- Added module search, category filters, per-module settings routing, reset layout, and settings export/import controls.
- Added server profile manager with saved IP/port entries, favorites, recent timestamp support, notes, categories, safe ping/status checks, overlay preset fields, keybind preset fields, and recommended pack notes.
- Added Packs tab for importing `.mcpack`, `.mcaddon`, and `.zip` files into Bomb Client-managed storage, toggling managed packs, and opening Bedrock development pack folders.
- Added optional Bomb Server Bridge foundation with URL settings, Offline / Waiting / Connected status, disabled-by-default behavior, mock/test mode, and documented JSON payload examples.
- Added overlay data context so overlays can use safe local polling, server profiles, foreground-window checks, ping/status checks, mock data, and future bridge data.
- Added Latest Updates tab showing installed version, latest version, release date, update status, changelog, and GitHub release link.
- Added `--self-test` coverage for module loading, overlay registration, settings save/load, update manifest parsing, latest update changelog parsing, server profiles, pack config, and bridge mock start/stop.

### Changed

- Bumped app and assembly version to `2.0.0`.
- Updated `build.ps1` to compile every `.cs` file in `src` so future systems can live in separate files.
- Preserved the Feather-style dark launcher shell, profiles, account UI, external overlay behavior, and client visual layer.
- Reworded pack/visual language so public-server strategy centers on external overlays and server profiles, with packs treated as optional managed files.

### Fixed

- Kept visual features in Bomb Client's external visual layer instead of making them depend on a server-overridden pack.
- Latest update metadata now has explicit release title, release date, changelog, GitHub release URL, and download URL fields.

### Known Limitations

- Bomb Client still cannot read live Bedrock inventory, potion, armor durability, NBT, or server-only match state from public servers unless a server owner opts into a compatible bridge.
- Bridge networking is a documented foundation with mock/test data in this release; production cooperating-server plugins can target the documented JSON/HTTP/WebSocket shape later.
- Optional generated packs are only for worlds or servers that intentionally allow packs. They are not the public-server visual strategy.

### Upgrade Notes

- This is a required update through `update.json`.
- Existing overlay settings remain supported.
- New module, server profile, pack, and bridge config files are stored under `%APPDATA%\BombClient`.

### Release Checklist For Future Updates

- Update `AppInfo.Version`.
- Update assembly and file versions.
- Update `update.json`.
- Update `CHANGELOG.md`.
- Create versioned GitHub release notes in `release_notes/`.
- Ensure the Latest Updates tab shows the new changelog.
