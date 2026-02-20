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

  // старт игры
  setTimeout(() => {
    ws.send(JSON.stringify({ type: "game.start", payload: {} }));
  }, 500);

  // бросок кубика
  setTimeout(() => {
    ws.send(JSON.stringify({ type: "game.roll_dice", payload: {} }));
  }, 1200);
});

ws.on("message", (m) => {
  const msg = JSON.parse(m.toString());
  console.log("<<", msg);

  // когда пришёл dice — двигаемся на это число
  if (msg.type === "game.dice_rolled") {
    ws.send(JSON.stringify({ type: "game.move", payload: { steps: msg.payload.value } }));
  }
});

ws.on("close", () => console.log("WS close"));
ws.on("error", (e) => console.error("WS err", e));