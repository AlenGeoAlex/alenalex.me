/**
 * How a reaction moves over a note while it's hovered:
 *   float   rises and sways, like live-stream hearts
 *   rise    small and quick, flickering upwards (embers, steam)
 *   burst   pops in place, spins and shrinks away
 *   crawl   walks across the bottom of the note
 *   bounce  hops along in arcs
 *   peek    pops up somewhere, blinks, and goes
 */
export type ReactionEffect = 'float' | 'rise' | 'burst' | 'crawl' | 'bounce' | 'peek';

export interface Reaction {
  /** as stored by the API (GuestbookReactions in api/) */
  key: string;
  label: string;
  effect: ReactionEffect;
}

/**
 * The reactions Alen can put on a note from Discord. To add one: add it here and to
 * GuestbookReactions in the API, and render its object into public/objects/reactions/<key>.webp
 * (site/tools/objects, ?set=reactions).
 */
export const REACTIONS: readonly Reaction[] = [
  { key: 'heart', label: 'heart', effect: 'float' },
  { key: 'fire', label: 'fire', effect: 'rise' },
  { key: 'laugh', label: 'laugh', effect: 'bounce' },
  { key: 'clap', label: 'clap', effect: 'burst' },
  { key: 'coffee', label: 'coffee', effect: 'rise' },
  { key: 'bug', label: 'bug', effect: 'crawl' },
  { key: 'hundred', label: 'hundred', effect: 'burst' },
  { key: 'thanks', label: 'thanks', effect: 'float' },
  { key: 'eyes', label: 'eyes', effect: 'peek' },
  { key: 'sparkles', label: 'sparkles', effect: 'burst' },
];

const BY_KEY = new Map(REACTIONS.map((r) => [r.key, r]));

/** Known reactions only, in the order given; unknown keys (from a newer API) are skipped. */
export function reactionsFor(keys: readonly string[]): Reaction[] {
  return keys.map((k) => BY_KEY.get(k)).filter((r): r is Reaction => !!r);
}

export const reactionIcon = (key: string) => `/objects/reactions/${key}.webp`;
