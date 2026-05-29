# Bomb Client

Current version: `2.0.0`

Bomb Client is a Windows desktop launcher for Minecraft Bedrock with external PvP overlays, profiles, server tools, managed packs, account UI, visuals, and update checking. It launches Bedrock and draws optional click-through HUD windows above the game without injecting into Minecraft, editing memory, modifying packets, or changing the installed Microsoft Store app.

## UI

Bomb Client uses a Feather-style dark launcher shell with Play, Modules, Visuals, Servers, Packs, and Updates tabs, plus profile/settings/window controls. The top-right icon cluster keeps consistent spacing as the window stretches.

The maximize button stretches the frameless launcher to the Windows working area, so the taskbar remains visible. Press `F11` for true fullscreen and press `F11` again or `Esc` to leave fullscreen.

## Run

Open:

`%APPDATA%\BombClient\Build\Bomb Client.exe`

## Included overlays

- FPS
- Ping
- CPS
- Keystrokes
- Combo
- Crosshair
- Clock
- Session timer
- Minecraft RAM
- System stats
- Server info
- Game status
- Armor HUD
- Potion Effects
- Hotbar Preview
- Shulker Preview

Each overlay is now a module with its own toggle in the Modules tab. Turn on HUD edit mode to drag overlay windows, then turn it off to make them click-through again.

By default, overlays only appear during active Minecraft gameplay. Bomb Client checks that Minecraft is the foreground app and that the Windows cursor is hidden, so the HUD stays out of the main menu, pause menu, inventory/chat-style menus, and other apps. You can change this in Settings.

The overlay list is limited to two columns and scrolls vertically so every module stays reachable on normal and maximized windows.

## Profiles

The Profiles tab can launch:

- Minecraft Bedrock Release
- Minecraft Preview
- A custom shortcut, exe, URI, or external Bedrock version manager

Bedrock on Windows does not expose the same open per-version installation system that Java launchers use, so Bomb Client uses safe launch profiles instead of patching or replacing the installed Microsoft Store app.

## Modules

Bomb Client 2.0 adds a module architecture for overlays, visuals, account, profiles, servers, packs, bridge, updates, and future features. Modules have ids, display names, descriptions, categories, enabled states, settings metadata, and saved config.

The Modules tab includes search, category filters, module settings routing, reset layout, and settings export/import controls.

## Servers

The Servers tab supports public Bedrock servers safely and externally:

- saved IP/port profiles
- favorites and recent timestamp support
- notes
- categories: PvP, SMP, Practice, Featured, Custom
- per-server overlay/keybind preset fields
- recommended pack notes
- normal safe ping/status checks
- quick active-server selection

Bomb Client does not modify traffic, fake server data, inject into Minecraft, or read Bedrock memory. Advanced server data is unavailable unless the server owner intentionally supports Bomb Server Bridge.

## Packs

The Packs tab can import `.mcpack`, `.mcaddon`, and `.zip` files into Bomb Client-managed storage, enable/disable managed entries, and open Bedrock development resource/behavior pack folders.

Packs are only one optional part of Bomb Client. Public-server support comes from safe external overlays, profiles, notes, local stats, and optional cooperating-server bridge data. Bomb Client visuals remain part of the external client layer.

## Bomb Server Bridge

Bomb Server Bridge is an optional, disabled-by-default foundation for cooperating servers. A server owner must install or enable a compatible plugin, add-on, script, or server bridge before advanced server data can populate overlays.

Supported bridge data is safe, server-authoritative overlay text such as scoreboard lines, current kit, match state, arena, team info, cooldowns, server messages, and non-sensitive player stats. Bomb Client 2.0 includes bridge URL settings, status, mock/test mode, and documented JSON examples in `docs/bomb-server-bridge.md`.

## Account

The Account tab includes a **Sign in with Microsoft** button that uses Microsoft's official device-code authentication flow. After the browser sign-in completes, Bomb Client reads the Microsoft Graph profile display name and email, then shows them in the Account tab.

Microsoft requires an OAuth app registration client ID before a desktop app can read profile data. Paste that Application (client) ID into the Account tab once, then use Sign in with Microsoft. Bomb Client does not collect, store, or handle your Microsoft password or account tokens; it stores only the display name, email, and connection time.

## External Overlay Limits

Bomb Client does not inject into Bedrock or read game memory. Live armor durability, potion effects, hotbar contents, and shulker NBT contents are not exposed to external Windows apps by Minecraft Bedrock. The matching overlay modules show unavailable/mock/bridge data states; real population requires an opt-in cooperating-server bridge.

## Latest Updates

The Latest Updates tab shows the installed version, latest available version, update status, release date, changelog, and GitHub release link from `update.json` metadata.

## Updates

Bomb Client checks this repository's `update.json` on startup:

`https://api.github.com/repos/EnderKraken914/bomb-client/contents/update.json?ref=main`

When `required_version` is higher than the installed app version and `force_update` is `true`, players must update before the launcher opens. The app opens the public download URL for the versioned zip attached to the GitHub Release and exits instead of replacing its own executable.

To reduce antivirus false positives, Bomb Client uses foreground-only input polling for CPS/keystrokes instead of global keyboard or mouse hooks, and it does not run a self-updating installer script.

To force an update:

1. Build a new `BombClient-Windows.zip`.
2. Upload it to a versioned GitHub Release like `v2.0.0`.
3. Update `update.json` with the new `latest_version`, `required_version`, and Release asset URL.
4. Update `CHANGELOG.md` and `release_notes/`.
5. Commit and push the updated files.

## Client Visuals

The Visuals tab controls Bomb Client's own external visual layer instead of building or importing a `.mcpack`. These visuals are drawn by the launcher above Bedrock, so they do not take a server resource-pack slot and cannot be overridden by server packs.

Visual size sliders are included for shield, totem item, and totem pop markers. Because Bomb Client still does not inject into Bedrock or read game memory, this layer can draw over the game but cannot erase Bedrock-rendered pixels or replace item models.
