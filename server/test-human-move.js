const WebSocket = require("ws");

const token = process.env.TOKEN;
const roomId = process.env.ROOM_ID;

const ws = new WebSocket(`ws://localhost:4000/ws?token=${token}`);

function send(type, payload = {}) {
  const msg = { type, payload };
  console.log(">>>", JSON.stringify(msg));
  ws.send(JSON.stringify(msg));
}

ws.on("open", () => {
  console.log("WS opened");
});

ws.on("message", (raw) => {
  const msg = JSON.parse(raw.toString());

  if (msg.type === "connected") {
    console.log("CONNECTED", msg.payload);
    send("room.join", { roomId });

    setTimeout(() => {
      send("game.move", { steps: 6 });
    }, 1000);

    return;
  }

  console.log("<<<", JSON.stringify(msg, null, 2));
});
