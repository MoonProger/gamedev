const fs = require("fs");
const path = require("path");

function getUnityScenePath() {
  return (
    process.env.UNITY_SCENE_PATH ||
    path.join(process.cwd(), "..", "unity", "BoardGame", "Assets", "Scenes", "то самое.unity")
  );
}

function getCharactersConfigPath() {
  return process.env.CHARACTERS_CONFIG_PATH || path.join(process.cwd(), "characters.config.json");
}

function toInt(raw) {
  const n = Number(String(raw ?? "").trim());
  return Number.isFinite(n) ? Math.trunc(n) : 0;
}

function pickField(block, key) {
  const m = block.match(new RegExp(`\\n\\s*${key}:\\s*([^\\n]+)`));
  return m?.[1];
}

function parseCharactersFromScene(scenePath) {
  if (!fs.existsSync(scenePath)) {
    throw new Error(`Unity scene not found: ${scenePath}`);
  }

  const raw = fs.readFileSync(scenePath, "utf-8");
  const startMarker = "m_Name: CharacterDatabase";
  const startIdx = raw.indexOf(startMarker);
  if (startIdx < 0) {
    throw new Error("CharacterDatabase not found in scene");
  }

  const snippet = raw.slice(startIdx, startIdx + 30000);
  const charactersSectionIdx = snippet.indexOf("characters:");
  if (charactersSectionIdx < 0) {
    throw new Error("characters section not found under CharacterDatabase");
  }

  const section = snippet.slice(charactersSectionIdx);
  const chunks = section.split(/\n\s*-\s+id:\s+/g).slice(1);

  return chunks
    .map((chunk) => {
      const lines = chunk.split("\n");
      const id = String(lines[0] ?? "").trim();
      if (!id) return null;

      const displayNameRaw = pickField(chunk, "displayName");
      const displayName = displayNameRaw ? String(displayNameRaw).replace(/^"|"$/g, "").trim() : "";

      return {
        id,
        displayName,
        money: toInt(pickField(chunk, "money")),
        experience: toInt(pickField(chunk, "experience")),
        success: toInt(pickField(chunk, "success")),
        volounteer: toInt(pickField(chunk, "volounteer")),
        science: toInt(pickField(chunk, "science")),
        art: toInt(pickField(chunk, "art")),
        media: toInt(pickField(chunk, "media")),
        business: toInt(pickField(chunk, "business")),
        sport: toInt(pickField(chunk, "sport")),
        tourism: toInt(pickField(chunk, "tourism")),
        it: toInt(pickField(chunk, "it")),
      };
    })
    .filter(Boolean);
}

function main() {
  const scenePath = getUnityScenePath();
  const outPath = getCharactersConfigPath();
  const characters = parseCharactersFromScene(scenePath);

  const payload = {
    version: "unity-characters-v1",
    generatedAt: new Date().toISOString(),
    sourceScene: scenePath,
    characters,
  };

  fs.writeFileSync(outPath, JSON.stringify(payload, null, 2) + "\n", "utf-8");
  console.log(`Exported ${characters.length} characters to ${outPath}`);
}

main();
