# Bomb Client

Current version: `1.0.1`

Bomb Client is a Windows desktop launcher for Minecraft Bedrock with external PvP overlays. It launches Bedrock, draws optional click-through HUD windows above the game, and builds a visual-only Bedrock resource pack without editing the installed game.

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

Each overlay has its own toggle in the Overlays tab. Turn on HUD edit mode to drag overlay windows, then turn it off to make them click-through again.

By default, overlays only appear during active Minecraft gameplay. Bomb Client checks that Minecraft is the foreground app and that the Windows cursor is hidden, so the HUD stays out of the main menu, pause menu, inventory/chat-style menus, and other apps. You can change this in Settings.

The overlay list is limited to two columns and scrolls vertically so every module stays reachable on normal and maximized windows.

## Profiles

The Profiles tab can launch:

- Minecraft Bedrock Release
- Minecraft Preview
- A custom shortcut, exe, URI, or external Bedrock version manager

Bedrock on Windows does not expose the same open per-version installation system that Java launchers use, so Bomb Client uses safe launch profiles instead of patching or replacing the installed Microsoft Store app.

## Account

The Account tab opens safe Microsoft sign-in surfaces:

- Minecraft's own sign-in flow
- Xbox app
- Windows account settings
- Minecraft and Microsoft account web pages
- Microsoft Store

Bomb Client does not collect, store, or handle your Microsoft password or tokens.

## Updates

Bomb Client checks this repository's `update.json` on startup:

`https://raw.githubusercontent.com/EnderKraken914/bomb-client/main/update.json`

When `required_version` is higher than the installed app version and `force_update` is `true`, players must update before the launcher opens. The app downloads `BombClient-Windows.zip` from the latest GitHub release and replaces the AppData build.

To force an update:

1. Build a new `BombClient-Windows.zip`.
2. Publish it as a GitHub release asset.
3. Update `update.json` with the new `latest_version` and `required_version`.
4. Commit and push `update.json`.

## Visual pack

The Visual Pack tab can build/import `BombClientPvPPack.mcpack` with low fire, no bobber, clean pumpkin, and clear vignette options. The pack is generated under `%APPDATA%\BombClient\GeneratedPacks`.
