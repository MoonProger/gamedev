import fs from "fs";
import path from "path";
import { z } from "zod";

export type ServerCharacter = {
  id: string;
  displayName: string;
  money: number;
  experience: number;
  success: number;
  volounteer: number;
  science: number;
  art: number;
  media: number;
  business: number;
  sport: number;
  tourism: number;
  it: number;
};

const characterSchema = z.object({
  id: z.string().min(1),
  displayName: z.string().optional().default(""),
  money: z.number().int(),
  experience: z.number().int(),
  success: z.number().int(),
  volounteer: z.number().int(),
  science: z.number().int(),
  art: z.number().int(),
  media: z.number().int(),
  business: z.number().int(),
  sport: z.number().int(),
  tourism: z.number().int(),
  it: z.number().int(),
});

const charactersConfigSchema = z.object({
  version: z.string().min(1),
  generatedAt: z.string().optional(),
  sourceScene: z.string().optional(),
  characters: z.array(characterSchema),
});

let cachedCharacters: ServerCharacter[] | null | undefined;

function getCharactersConfigPath(): string {
  return process.env.CHARACTERS_CONFIG_PATH || path.join(process.cwd(), "characters.config.json");
}

function loadCharactersFromConfig(): ServerCharacter[] {
  const configPath = getCharactersConfigPath();
  if (!fs.existsSync(configPath)) {
    throw new Error(`[characters.config] file not found: ${configPath}`);
  }

  const raw = fs.readFileSync(configPath, "utf-8");
  const parsed = charactersConfigSchema.safeParse(JSON.parse(raw));
  if (!parsed.success) {
    throw new Error(`[characters.config] invalid structure in: ${configPath}\n${parsed.error.message}`);
  }

  const byId = new Set<string>();
  for (const ch of parsed.data.characters) {
    if (byId.has(ch.id)) {
      throw new Error(`[characters.config] duplicate character id: '${ch.id}'`);
    }
    byId.add(ch.id);
  }

  return parsed.data.characters as ServerCharacter[];
}

export function getCharacterTemplates(): ServerCharacter[] {
  if (cachedCharacters !== undefined) return cachedCharacters ?? [];
  cachedCharacters = loadCharactersFromConfig();
  return cachedCharacters ?? [];
}
