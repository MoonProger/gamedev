# Unity Multiplayer Protocol v1

This document is the single source of truth for realtime messages between:

- Unity client (through Web bridge),
- Web app (`web`),
- Server (`server`).

The protocol is Unity-first: gameplay state is authoritative on server, Unity renders and sends player intents.

## 1) Transport and auth

- Transport: WebSocket
- URL: `/ws?token=<JWT>`
- Encoding: UTF-8 JSON
- Every message has envelope:

```json
{
  "type": "message.type",
  "payload": {}
}
```

## 2) Envelope rules

- `type` is required and string.
- `payload` should be an object (use `{}` if no fields).
- Unknown `type` -> server emits `error`.
- Invalid JSON -> server emits `error` (`Bad JSON`).

## 3) Client -> Server (`WsIn`)

### Room / lobby

- `room.join`
  - payload: `{ "roomId": "string", "password": "string?" }`
- `room.leave`
  - payload: `{}`
- `room.ready`
  - payload: `{ "ready": true }`
- `ping`
  - payload: any (optional)

### Game lifecycle

- `game.start`
  - payload: `{}`
- `game.roll_dice`
  - payload: `{}`
- `game.move`
  - payload: `{ "steps": 1..6, "toSector?": number }`
  - If server has `strictMoveValidation=true` in board config, `toSector` becomes required.
- `game.card`
  - payload: `{ "cardId?": "string" }` (optional intent hint)
  - Server ignores client effects and resolves drawn card + outcomes authoritatively.
- `game.project`
  - payload: `{ "projectId?": "string", "successPoints?": number }`
  - `successPoints` is ignored by server in authoritative mode.

## 4) Server -> Client (`WsOut`)

### Connection / room

- `connected`
  - payload: `{ "userId": "string" }`
- `room.state`
  - payload: room DTO (see `rooms.dto.ts`)
- `room.player_joined`
  - payload: `{ "userId": "string" }`
- `room.player_left`
  - payload: `{ "userId": "string" }`
- `pong`
  - payload: passthrough / null

### Game events

- `game.started`
  - payload: `{ "activePlayerId": "string" }`
- `game.state`
  - payload: full game snapshot (authoritative)
- `game.dice_rolled`
  - payload: `{ "value": number }`
- `game.move`
  - payload: `{ "playerId": "string", "fromSector": number, "toSector": number, "dice": number }`
- `game.token_moved`
  - payload: `{ "playerId": "string", "pos": number, "steps": number }`
- `game.card`
  - payload:
    - `playerId`, `cardId`, `cardType`, `imageGuid`, `deckKey`
    - `deltas` (acting player stat deltas)
    - `affectedPlayers[]` snapshots for all players changed by effects (e.g. green coop)
    - `chainedCards[]` for draw-next effects
    - `checks` (blue/red/green deterministic checks)
    - `grants`, `playerState`, plus compatibility fields (`scores`, `money`, `experience`)
- `game.project`
  - payload:
    - `playerId`, `projectId`, `successPoints`, `totalSuccess`
    - `payment` (`grant` or `money`)
    - `grants`, `money`, `completedProjects[]`, `playerState`
- `game.turn_changed`
  - payload: `{ "activePlayerId": "string" }`
- `game.finished`
  - payload: `{ "winnerUserId": "string", "finalScores": object }`
- `game.paused`
  - payload: `{ "reason": "string" }`
- `game.resumed`
  - payload: `{}`

### Errors

- `error`
  - payload: `{ "message": "string" }`

## 5) Unity bridge mapping (web `Game.tsx`)

Current mapping from server events to Unity `GameManager` methods:

- `game.state` -> `UpdateGameState(JSON.stringify(payload))`
- `game.dice_rolled` -> `OnDiceRolled(payload.value)`
- `game.move` -> `OnPlayerMove(JSON.stringify(payload))`
- `game.card` -> `OnCardPlayed(JSON.stringify(payload))`
- `game.project` -> `OnProjectCompleted(JSON.stringify(payload))`
- `game.turn_changed` -> `OnTurnChanged(payload.activePlayerId)`
- `game.token_moved` -> `OnTokenMoved(JSON.stringify(payload))`
- `game.finished` -> `OnGameFinished(JSON.stringify(payload))`
- `game.paused` -> `OnGamePaused(JSON.stringify(payload))`
- `game.resumed` -> `OnGameResumed(JSON.stringify(payload))`

## 6) Versioning policy

- This is `v1`.
- Backward-compatible additions:
  - add optional fields in payload.
- Breaking changes:
  - new protocol version document (e.g. `v2`),
  - transitional compatibility window in server handlers.

## 7) Current known legacy/temporary parts

- `game.card.payload.cardId` from client is treated as optional intent hint.
- `game.project.payload.successPoints` from client is ignored.
- Green coop partner selection is server-deterministic (no interactive chooser yet).

## 8) Board synchronization

Server supports optional strict board validation via `server/board.config.json`:

- file includes sectors graph (`id`, `neighbors`), `startSector`, `strictMoveValidation`.
- when strict mode is enabled:
  - client must send `game.move.payload.toSector`,
  - server validates reachability in exactly `steps`.
