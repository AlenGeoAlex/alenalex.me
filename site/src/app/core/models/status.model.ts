export type DiscordStatus = 'online' | 'idle' | 'dnd' | 'offline' | 'unknown';

/** GET /api/status */
export interface LiveStatus {
  discord: { status: DiscordStatus; activity: string | null };
  spotify: { track: string; artist: string; album: string | null; artUrl: string | null } | null;
  /** the last few Spotify tracks, newest first (the current one included) */
  recentTracks: RecentTrack[];
  homelab: { up: number; total: number; services: { name: string; up: boolean; latencyMs: number | null }[] };
  fetchedAt: string;
}

export interface RecentTrack {
  track: string;
  artist: string;
  album: string | null;
  artUrl: string | null;
  /** when it started playing */
  playedAt: string;
}
