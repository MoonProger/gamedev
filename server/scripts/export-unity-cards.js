const fs = require("fs");
const path = require("path");

const CARD_DATABASE_SCRIPT_GUID = "1afa0cb99074e8944b5041da372a5e9b";

function getUnityScenePath() {
  return (
    process.env.UNITY_SCENE_PATH ||
    path.join(process.cwd(), "..", "unity", "BoardGame", "Assets", "Scenes", "то самое.unity")
  );
}

function getCardsConfigPath() {
  return process.env.CARDS_CONFIG_PATH || path.join(process.cwd(), "cards.config.json");
}

function parseIntFromLine(line) {
  const m = line.match(/:\s*(-?\d+)\s*$/);
  if (!m) return null;
  const n = Number(m[1]);
  return Number.isFinite(n) ? n : null;
}

function parseGuidFromImageLine(line) {
  const m = line.match(/guid:\s*([a-f0-9]{32})/i);
  return m && m[1] ? m[1] : null;
}

function lineIndent(line) {
  const m = line.match(/^(\s*)/);
  return m ? m[1].length : 0;
}

function parseDecksFromUnityScene(scenePath) {
  if (!fs.existsSync(scenePath)) {
    throw new Error(`Unity scene not found: ${scenePath}`);
  }

  const lines = fs.readFileSync(scenePath, "utf-8").split(/\r?\n/);
  const scriptLineIdx = lines.findIndex((line) =>
    line.includes(`m_Script: {fileID: 11500000, guid: ${CARD_DATABASE_SCRIPT_GUID}, type: 3}`)
  );
  if (scriptLineIdx < 0) {
    throw new Error("CardDatabase component not found in scene");
  }

  const decksStart = lines.findIndex((line, idx) => idx > scriptLineIdx && line.trim() === "decks:");
  if (decksStart < 0) {
    throw new Error("decks section not found under CardDatabase");
  }

  const decks = {};
  let deckKey = null;
  let card = null;
  let lane = null;
  let effect = null;
  let cardIndex = 0;

  const flushEffect = () => {
    if (!card || !effect || !lane) return;
    card[lane].push(effect);
    effect = null;
  };

  const flushCard = () => {
    flushEffect();
    if (!deckKey || !card) return;
    decks[deckKey].push(card);
    card = null;
    lane = null;
  };

  for (let i = decksStart + 1; i < lines.length; i++) {
    const line = lines[i];
    if (line.startsWith("--- ")) break;

    const indent = lineIndent(line);
    const trimmed = line.trim();
    if (!trimmed) continue;

    if (indent === 2 && trimmed.startsWith("- key:")) {
      flushCard();
      deckKey = trimmed.slice("- key:".length).trim().toLowerCase();
      cardIndex = 0;
      if (!decks[deckKey]) decks[deckKey] = [];
      continue;
    }

    if (indent === 4 && trimmed.startsWith("- cardType:")) {
      flushCard();
      if (!deckKey) continue;
      card = {
        id: `${deckKey}:${cardIndex++}`,
        deckKey,
        cardType: parseIntFromLine(trimmed) ?? 0,
        imageGuid: null,
        effects: [],
        soloEffects: [],
        coopEffects: [],
      };
      lane = null;
      continue;
    }

    if (!card) continue;

    if (indent === 6 && trimmed.startsWith("image:")) {
      card.imageGuid = parseGuidFromImageLine(trimmed);
      continue;
    }

    if (
      indent === 6 &&
      (trimmed.startsWith("effects:") ||
        trimmed.startsWith("soloEffects:") ||
        trimmed.startsWith("coopEffects:"))
    ) {
      flushEffect();
      if (trimmed.endsWith("[]")) {
        lane = null;
      } else if (trimmed.startsWith("effects:")) {
        lane = "effects";
      } else if (trimmed.startsWith("soloEffects:")) {
        lane = "soloEffects";
      } else {
        lane = "coopEffects";
      }
      continue;
    }

    if (indent === 6 && trimmed.startsWith("- effect:")) {
      flushEffect();
      if (!lane) continue;
      effect = {
        effect: parseIntFromLine(trimmed) ?? 0,
        condition: 0,
        statName: 0,
        amount: 0,
      };
      continue;
    }

    if (indent === 8 && effect) {
      if (trimmed.startsWith("condition:")) {
        effect.condition = parseIntFromLine(trimmed) ?? 0;
      } else if (trimmed.startsWith("statName:")) {
        effect.statName = parseIntFromLine(trimmed) ?? 0;
      } else if (trimmed.startsWith("amount:")) {
        effect.amount = parseIntFromLine(trimmed) ?? 0;
      }
    }
  }

  flushCard();
  return decks;
}

function main() {
  const scenePath = getUnityScenePath();
  const outPath = getCardsConfigPath();
  const decks = parseDecksFromUnityScene(scenePath);

  const payload = {
    version: "unity-cards-v1",
    generatedAt: new Date().toISOString(),
    sourceScene: scenePath,
    decks,
  };

  fs.writeFileSync(outPath, JSON.stringify(payload, null, 2) + "\n", "utf-8");

  const totalDecks = Object.keys(decks).length;
  const totalCards = Object.values(decks).reduce((sum, cards) => sum + cards.length, 0);
  console.log(`Exported ${totalCards} cards across ${totalDecks} decks to ${outPath}`);
}

main();
