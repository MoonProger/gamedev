const WebSocket = require("ws");

const token = process.env.TOKEN;
const roomId = process.env.ROOM_ID;

if (!token || !roomId) {
  console.error("TOKEN and ROOM_ID are required");
  process.exit(1);
}

const ws = new WebSocket(`ws://localhost:4000/ws?token=${token}`);

let latestState = null;
let myUserId = null;

const BOARD = {
  0: [3, 4, 8],
  1: [5, 7, 8],
  2: [9, 10, 11],
  3: [0, 4, 10],
  4: [0, 3, 10],
  5: [1, 6, 7],
  6: [5, 9, 11],
  7: [1, 5, 8],
  8: [0, 1, 7],
  9: [2, 6, 11],
  10: [2, 3, 4],
  11: [2, 6, 9],
};

function findAutoMoveTarget(fromSector, steps) {
  function dfs(current, stepsLeft, visited) {
    if (stepsLeft === 0) return current;

    const neighbors = [...(BOARD[current] ?? [])].sort((a, b) => a - b);

    for (const next of neighbors) {
      if (visited.has(next)) continue;

      visited.add(next);
      const candidate = dfs(next, stepsLeft - 1, visited);
      visited.delete(next);

      if (candidate !== null) return candidate;
    }

    return null;
  }

  return dfs(fromSector, steps, new Set([fromSector]));
}

function send(type, payload = {}) {
  const msg = { type, payload };
  console.log(">>>", JSON.stringify(msg));
  ws.send(JSON.stringify(msg));
}

function shortState(game) {
  if (!game) return;

  const history = Array.isArray(game.history) ? game.history : [];
  const lastEvents = history.slice(-8).map((e) => ({
    type: e.type,
    playerId: e.playerId,
    activePlayerId: e.activePlayerId,
    dice: e.dice,
    steps: e.steps,
    fromSector: e.fromSector,
    toSector: e.toSector,
    characterId: e.characterId,
  }));

  console.log("STATE:", {
    phase: game.phase,
    started: game.started,
    activePlayerId: game.activePlayerId,
    lastDice: game.lastDice,
    possibleMoves: game.possibleMoves,
    positions: game.positions,
    lastEvents,
  });
}

ws.on("open", () => {
  console.log("WS opened");
});

ws.on("message", (raw) => {
  const msg = JSON.parse(raw.toString());

  if (msg.type === "connected") {
    myUserId = msg.payload.userId;
    console.log("CONNECTED:", msg.payload);
    send("room.join", { roomId });
    return;
  }

  if (msg.type === "room.state") {
    console.log("ROOM STATE:");
    console.log(JSON.stringify(msg.payload.players, null, 2));
    return;
  }

  if (msg.type === "game.state") {
    latestState = msg.payload;
    shortState(latestState);
    return;
  }

  if (msg.type === "game.turn_timer") {
    console.log("TIMER:", msg.payload);
    return;
  }

  console.log("<<<", JSON.stringify(msg, null, 2));
});

setTimeout(() => {
  console.log("\n--- START GAME ---");
  send("game.start", {});
}, 1500);

setTimeout(() => {
  console.log("\n--- SELECT CHARACTER FOR HUMAN PLAYER ---");
  send("game.character_select", {
    characterId: "manual-test-character",
    stats: {
      money: 50,
      experience: 20,
      success: 0,
      volounteer: 10,
      science: 10,
      art: 10,
      media: 10,
      business: 10,
      sport: 10,
      tourism: 10,
      it: 10
    }
  });
}, 3000);

setTimeout(() => {
  console.log("\n--- HUMAN ROLL DICE IF ACTIVE ---");
  if (latestState && latestState.activePlayerId === myUserId) {
    send("game.roll_dice", {});
  } else {
    console.log("Human is not active player now, skip manual roll");
  }
}, 5000);

setTimeout(() => {
  console.log("\n--- HUMAN MOVE IF POSSIBLE ---");

  if (!latestState || latestState.activePlayerId !== myUserId) {
    console.log("Human is not active player now");
    return;
  }

  if (latestState.phase !== "WAITING_MOVE") {
    console.log("Human is not in WAITING_MOVE phase");
    return;
  }

  const steps = Number(latestState.lastDice);

  if (!Number.isFinite(steps) || steps < 1) {
    console.log("No valid lastDice found in game state");
    return;
  }

  const fromSector = Number(latestState.positions?.[myUserId] ?? 9);
  const toSector = findAutoMoveTarget(fromSector, steps);

  if (!Number.isFinite(toSector)) {
    console.log("No valid toSector found", { fromSector, steps });
    return;
  }

  send("game.move", {
    steps,
    toSector,
  });
}, 7000);

setTimeout(() => {
  console.log("\n--- FINAL STATE SNAPSHOT ---");
  shortState(latestState);

  const history = latestState?.history ?? [];
  const botEvents = history.filter((e) =>
    ["character_select_bot", "roll_dice", "move", "card", "project", "card_closed"].includes(e.type)
  );

  console.log("\nRelevant events:");
  console.log(JSON.stringify(botEvents.slice(-20), null, 2));

  ws.close();
  process.exit(0);
}, 25000);
