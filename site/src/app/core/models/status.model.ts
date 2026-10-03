export type DiscordStatus = 'online' | 'idle' | 'dnd' | 'offline' | 'unknown';

/** GET /api/status */
export interface LiveStatus {
  discord: { status: DiscordStatus; activity: string | null };
  spotify: { track: string; artist: string; album: string | null; artUrl: string | null } | null;
  homelab: { up: number; total: number; services: { name: string; up: boolean; latencyMs: number | null }[] };
  fetchedAt: string;
}
