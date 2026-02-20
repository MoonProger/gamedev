const WebSocket = require("ws");

const token = process.env.TOKEN;
const roomId = process.env.ROOM_ID;

if (!token || !roomId) {
  console.log("Set env TOKEN and ROOM_ID");
  process.exit(1);
}

const ws = new WebSocket(`ws://localhost:4000/ws?token=${token}`);

ws.on("open", () => {
  console.log("WS open");
  ws.send(JSON.stringify({ type: "room.join", payload: { roomId } }));
  setTimeout(() => {
    ws.send(JSON.stringify({ type: "room.ready", payload: { ready: true } }));
  }, 500);
});

ws.on("message", (m) => console.log("<<", m.toString()));
ws.on("close", () => console.log("WS close"));
ws.on("error", (e) => console.error("WS err", e));
