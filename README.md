# Bomb Client

Current version: `1.1.2`

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
- Armor HUD
- Potion Effects
- Hotbar Preview
- Shulker Preview

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

## External Overlay Limits

Bomb Client does not inject into Bedrock or read game memory. Live armor durability, potion effects, hotbar contents, and shulker NBT contents are not exposed to external Windows apps by Minecraft Bedrock. The matching overlay modules are configurable HUD shells for layout, while real inventory/NBT population would require a server/add-on data bridge or game hooking.

## Updates

Bomb Client checks this repository's `update.json` on startup:

`https://raw.githubusercontent.com/EnderKraken914/bomb-client/main/update.json`

When `required_version` is higher than the installed app version and `force_update` is `true`, players must update before the launcher opens. The app opens the public download URL for the versioned zip in `release/` and exits instead of replacing its own executable.

To reduce antivirus false positives, Bomb Client uses foreground-only input polling for CPS/keystrokes instead of global keyboard or mouse hooks, and it does not run a self-updating installer script.

To force an update:

1. Build a new `BombClient-Windows.zip`.
2. Copy it to a versioned file like `release/BombClient-Windows-1.1.2.zip`.
3. Update `update.json` with the new `latest_version` and `required_version`.
4. Commit and push the updated files.

## Visual pack

The Visual Pack tab can build/import `BombClientPvPPack.mcpack` with low fire, no bobber, low shield, small totem, small totem pop, clean pumpkin, and clear vignette options. The pack is generated under `%APPDATA%\BombClient\GeneratedPacks`.

Visual size sliders are included for shield, totem item, and totem pop textures.
