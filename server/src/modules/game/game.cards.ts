import fs from "fs";
import path from "path";
import { z } from "zod";

type RawCardEffect = {
  effect: number;
  condition: number;
  statName: number;
  amount: number;
};

export type ServerCard = {
  id: string;
  deckKey: string;
  cardType: number;
  imageGuid: string | null;
  effects: RawCardEffect[];
  soloEffects: RawCardEffect[];
  coopEffects: RawCardEffect[];
};

type DecksByKey = Record<string, ServerCard[]>;

const effectSchema = z.object({
  effect: z.number().int().min(0),
  condition: z.number().int().min(0),
  statName: z.number().int().min(0),
  amount: z.number().int(),
});

const cardSchema = z.object({
  id: z.string().min(1),
  deckKey: z.string().min(1),
  cardType: z.number().int().min(0),
  imageGuid: z.string().regex(/^[a-f0-9]{32}$/i).nullable(),
  effects: z.array(effectSchema),
  soloEffects: z.array(effectSchema),
  coopEffects: z.array(effectSchema),
});

const cardsConfigSchema = z.object({
  version: z.string().min(1),
  generatedAt: z.string().optional(),
  sourceScene: z.string().optional(),
  decks: z.record(z.string().min(1), z.array(cardSchema)),
});

let cachedDecks: DecksByKey | null | undefined;

function getCardsConfigPath(): string {
  return process.env.CARDS_CONFIG_PATH || path.join(process.cwd(), "cards.config.json");
}

function loadDecksFromConfig(): DecksByKey | null {
  const configPath = getCardsConfigPath();
  if (!fs.existsSync(configPath)) {
    throw new Error(`[cards.config] file not found: ${configPath}`);
  }
  const raw = fs.readFileSync(configPath, "utf-8");
  const parsed = cardsConfigSchema.safeParse(JSON.parse(raw));
  if (!parsed.success) {
    throw new Error(`[cards.config] invalid structure in: ${configPath}\n${parsed.error.message}`);
  }

  // Extra integrity check: card.deckKey should match parent deck key.
  for (const [deckKey, cards] of Object.entries(parsed.data.decks)) {
    for (const card of cards) {
      if (card.deckKey !== deckKey) {
        throw new Error(
          `[cards.config] card '${card.id}' has deckKey='${card.deckKey}', expected '${deckKey}'`
        );
      }
    }
  }

  return parsed.data.decks as DecksByKey;
}

export function getCardDecks(): DecksByKey {
  if (cachedDecks !== undefined) return cachedDecks ?? {};
  cachedDecks = loadDecksFromConfig();
  return cachedDecks ?? {};
}

export function mapNodeTypeToDeckKey(nodeType?: string): string | null {
  if (!nodeType) return null;
  const normalized = nodeType.trim().toLowerCase();
  const map: Record<string, string> = {
    media: "media",
    business: "business",
    sport: "sport",
    it: "it",
    art: "art",
    science: "science",
    volounteer: "volounteer",
    tourism: "tourism",
    travel: "travel",
    grant: "grant_success",
  };
  return map[normalized] ?? null;
}

export function drawRandomCard(deckKey: string): ServerCard | null {
  const cards = getCardDecks()[deckKey];
  if (!cards || cards.length === 0) return null;
  return cards[Math.floor(Math.random() * cards.length)];
}
