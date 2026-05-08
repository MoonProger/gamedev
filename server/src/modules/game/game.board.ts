import fs from "fs";
import path from "path";

export type BoardSector = {
  id: number;
  neighbors: number[];
  label?: string;
  nodeType?: string;
};

export type BoardConfig = {
  version: string;
  strictMoveValidation: boolean;
  startSector: number;
  sectors: BoardSector[];
};

type BoardValidationResult = {
  ok: boolean;
  errors: string[];
};

let cachedBoard: BoardConfig | null | undefined;
let cachedBoardPath: string | null = null;

function getBoardPath(): string {
  return process.env.BOARD_CONFIG_PATH || path.join(process.cwd(), "board.config.json");
}

function validateBoardConfig(config: BoardConfig): BoardValidationResult {
  const errors: string[] = [];
  const ids = new Set<number>();

  if (!Array.isArray(config.sectors) || config.sectors.length === 0) {
    errors.push("sectors must be a non-empty array");
    return { ok: false, errors };
  }

  for (const sector of config.sectors) {
    if (!Number.isInteger(sector.id) || sector.id < 0)
      errors.push(`sector.id invalid: ${sector.id}`);
    if (ids.has(sector.id))
      errors.push(`duplicate sector.id: ${sector.id}`);
    ids.add(sector.id);

    if (!Array.isArray(sector.neighbors))
      errors.push(`sector ${sector.id}: neighbors must be array`);
    else if (sector.neighbors.some((n) => !Number.isInteger(n) || n < 0))
      errors.push(`sector ${sector.id}: neighbors contain non-integer or negative values`);
  }

  if (!ids.has(config.startSector))
    errors.push(`startSector ${config.startSector} not found in sectors`);

  for (const sector of config.sectors) {
    for (const n of sector.neighbors) {
      if (!ids.has(n))
        errors.push(`sector ${sector.id}: neighbor ${n} not found in sectors`);
    }
  }

  return { ok: errors.length === 0, errors };
}

function loadBoardConfig(): BoardConfig | null {
  const boardPath = getBoardPath();
  cachedBoardPath = boardPath;

  if (!fs.existsSync(boardPath))
    return null;

  const raw = fs.readFileSync(boardPath, "utf-8");
  const parsed = JSON.parse(raw) as BoardConfig;
  const validation = validateBoardConfig(parsed);
  if (!validation.ok) {
    throw new Error(`[board.config] invalid config:\n- ${validation.errors.join("\n- ")}`);
  }

  return parsed;
}

export function getBoardConfig(): BoardConfig | null {
  if (cachedBoard !== undefined)
    return cachedBoard;
  cachedBoard = loadBoardConfig();
  return cachedBoard;
}

export function getBoardStartSector(): number {
  const config = getBoardConfig();
  return config?.startSector ?? 0;
}

export function isStrictBoardValidationEnabled(): boolean {
  const config = getBoardConfig();
  return Boolean(config?.strictMoveValidation);
}

export function getBoardSectorById(id: number): BoardSector | null {
  const config = getBoardConfig();
  if (!config) return null;
  return config.sectors.find((s) => s.id === id) ?? null;
}

export function getBoardValidationInfo(): {
  path: string;
  loaded: boolean;
  strictMoveValidation: boolean;
  sectorsCount: number;
} {
  const config = getBoardConfig();
  return {
    path: cachedBoardPath || getBoardPath(),
    loaded: config != null,
    strictMoveValidation: Boolean(config?.strictMoveValidation),
    sectorsCount: config?.sectors.length ?? 0,
  };
}

export function isMoveReachable(from: number, to: number, steps: number): boolean {
  const config = getBoardConfig();
  if (!config)
    return false;
  if (steps < 0)
    return false;
  if (from === to && steps === 0)
    return true;

  const neighborsById = new Map<number, number[]>();
  for (const sector of config.sectors) {
    neighborsById.set(sector.id, sector.neighbors);
  }
  if (!neighborsById.has(from) || !neighborsById.has(to))
    return false;

  const visited = new Set<number>([from]);
  return dfsReachable(neighborsById, from, to, steps, visited);
}

function dfsReachable(
  neighborsById: Map<number, number[]>,
  current: number,
  target: number,
  stepsLeft: number,
  visited: Set<number>
): boolean {
  if (stepsLeft === 0)
    return current === target;

  const neighbors = neighborsById.get(current) ?? [];
  for (const next of neighbors) {
    if (visited.has(next))
      continue;

    visited.add(next);
    const ok = dfsReachable(neighborsById, next, target, stepsLeft - 1, visited);
    visited.delete(next);

    if (ok)
      return true;
  }
  return false;
}

