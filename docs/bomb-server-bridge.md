# Bomb Server Bridge

Bomb Server Bridge is an optional cooperating-server interface for servers that intentionally support Bomb Client.

It must be installed or enabled by the server owner. Bomb Client must never use packet sniffing, traffic modification, memory reading, injection, hooks, or fake server-side data to populate bridge-only overlays.

## Transport

Compatible servers may expose either:

- HTTP polling endpoint
- WebSocket stream
- localhost bridge from a server-side tool

Bomb Client 2.0 implements settings, status, and mock/test data. Production bridge clients should treat this document as the stable payload shape to target next.

## Status

- `Offline`: bridge disabled.
- `Waiting`: bridge enabled, no cooperating server data connected.
- `Connected`: mock/test data active, or a future bridge transport is connected.

## Example Message

```json
{
  "type": "bomb.overlay",
  "protocol_version": 1,
  "server": "example.net:19132",
  "arena": "Bridge Practice",
  "kit": "Pots",
  "match_state": "In match",
  "scoreboard": [
    "Bomb Bridge",
    "Round 2",
    "Red 1 - Blue 0"
  ],
  "party": {
    "team": "Red",
    "members": ["PlayerOne", "PlayerTwo"]
  },
  "cooldowns": {
    "pearl": "00:07",
    "totem": "ready"
  },
  "message": "Server-authoritative test payload",
  "player_stats": {
    "kills": 3,
    "streak": 2
  }
}
```

## Data Rules

- The server is authoritative for bridge fields.
- Bomb Client should show bridge data as unavailable when no bridge is connected.
- Public servers without bridge support still work with external overlays, local stats, safe ping, profiles, and notes.
- Bridge messages must not include sensitive tokens, passwords, private IPs, or anti-cheat bypass data.
