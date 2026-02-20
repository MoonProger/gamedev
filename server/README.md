# GameDev Server (HTTP + WebSocket)

## Setup
1. Create `.env` in `server/`:

DATABASE_URL="postgresql://postgres:0000@localhost:5432/gamedev?schema=public"
JWT_SECRET="super_secret_key_123456"
ROOM_ID="cmlv9b58z000028v84dc75fer"
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOiJjbWx2OG12YnMwMDAwOTB2OGVvMHNzbnJyIiwiZW1haWwiOiJ0ZXN0QHRlc3QuY29tIiwiaWF0IjoxNzcxNjEzOTUzLCJleHAiOjE3NzIyMTg3NTN9.yn9KZvyqR3qFccnIxqwAbkXgohA9Y7Ha9PVoW7YSWug"
PORT=4000

2. Migrate + generate:

npx prisma migrate dev --name init --config ./prisma.config.cjs
npx prisma generate --config ./prisma.config.cjs

3. Run:

npm run dev


## HTTP API
### Auth
- `POST /auth/register` `{ email, username, password }`
- `POST /auth/login` `{ email, password }` -> `{ token }`
- `GET /auth/me` header `Authorization: Bearer <token>`

### Rooms
- `POST /rooms` (auth) `{ title, settings }`
- `GET /rooms`
- `GET /rooms/:id`
- `POST /rooms/:id/join` (auth)
- `POST /rooms/:id/leave` (auth)
- `POST /rooms/:id/ready` (auth) `{ ready: boolean }`

## WebSocket
Connect:
`ws://localhost:4000/ws?token=<JWT>`

Incoming messages:
- `room.join` `{ roomId }`
- `room.leave` `{}`
- `room.ready` `{ ready }`
- `game.start` `{}`
- `game.roll_dice` `{}`
- `game.move` `{ steps }`

Server events:
- `connected` `{ userId }`
- `room.state` `{ room dto }`
- `room.player_joined` `{ userId }`
- `room.player_left` `{ userId }`
- `game.started` `{ activePlayerId }`
- `game.dice_rolled` `{ value }`
- `game.token_moved` `{ playerId, pos, steps }`
- `game.turn_changed` `{ activePlayerId }`
- `game.state` `{ started, activePlayerId, positions, lastDice, phase }`