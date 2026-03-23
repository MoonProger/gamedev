const WebSocket = require("ws");

//const token = process.env.TOKEN;
//const roomId = process.env.ROOM_ID;

const token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOiJjbW4yd3Awa3cwMDAwdHJ5eW5xMjk2czk5IiwiZW1haWwiOiJ0ZXN0QHRlc3QuY29tIiwiaWF0IjoxNzc0MjU0MjY0LCJleHAiOjE3NzQ4NTkwNjR9.55yZmNOQi13upBbeBph1fJDv4xtu5J-4_ooxz5KTxkA";
const roomId = "cmn2x7nx60001h42d1a44wtzo";

if (!token || !roomId) {
  console.log("Set env TOKEN and ROOM_ID");
  process.exit(1);
}

const ws = new WebSocket(`ws://localhost:4000/ws?token=${token}`);

let startedSent = false;
let rollSent = false;
let actionSent = false;

ws.on("open", () => {
  console.log("WS open");
  ws.send(JSON.stringify({
    type: "room.join",
    payload: { roomId }
  }));
});

ws.on("message", (m) => {
  const msg = JSON.parse(m.toString());
  console.log("<<", msg);

  if (msg.type === "game.state") {
    const state = msg.payload;

    if (!state.started && !startedSent) {
      startedSent = true;
      ws.send(JSON.stringify({ type: "game.start", payload: {} }));
      return;
    }

    if (state.started && state.phase === "WAITING_ROLL" && !rollSent) {
      rollSent = true;
      ws.send(JSON.stringify({ type: "game.roll_dice", payload: {} }));
      return;
    }

    if (state.started && state.phase === "WAITING_MOVE" && state.lastDice) {
      ws.send(JSON.stringify({
        type: "game.move",
        payload: { steps: state.lastDice }
      }));
      return;
    }

    if (state.started && state.phase === "WAITING_ACTION" && !actionSent) {
      actionSent = true;

      ws.send(JSON.stringify({
        type: "game.project",
        payload: {
          projectId: "startup_pitch",
          successPoints: 4
        }
      }));

      return;
    }
  }
});

ws.on("close", () => console.log("WS close"));
ws.on("error", (e) => console.error("WS err", e));