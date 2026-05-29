# Bomb Client Development

## Release Workflow

Every future Bomb Client update must update all version and release metadata together:

1. Update `AppInfo.Version` in `src/BombClient.cs`.
2. Update `[assembly: AssemblyVersion]` and `[assembly: AssemblyFileVersion]`.
3. Update `update.json` with `latest_version`, `required_version`, `release_date`, `release_title`, `changelog`, `github_release_url`, and `download_url`.
4. Update `CHANGELOG.md`.
5. Add GitHub release notes under `release_notes/`.
6. Confirm the Latest Updates tab shows the new changelog.
7. Run `.\build.ps1`.
8. Run `%APPDATA%\BombClient\Build\Bomb Client.exe --self-test`.
9. Upload the versioned zip to the matching GitHub Release.

## Safety Boundary

Bomb Client must stay external and server-friendly.

Do not add:

- DLL injection
- process hooks
- game memory reads or writes
- packet manipulation
- traffic sniffing
- anti-cheat bypass code
- reverse-engineering logic for Minecraft Bedrock internals
- edits to the installed Microsoft Store Minecraft app

Use safe external sources only: user configuration, foreground-window checks, normal ping/status checks, local system stats, CPS/key polling while Minecraft is foreground, overlay presets, server notes, managed pack files, and opt-in cooperating-server bridge data.
