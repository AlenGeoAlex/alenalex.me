export type SearchKind = 'post' | 'part' | 'series';

/** A section of a post that matched, linked by its heading. */
export interface SearchSection {
  title: string;
  /** router path and fragment */
  path: string;
  fragment: string | null;
  /** HTML: escaped text with the matches in <mark> */
  excerpt: string;
}

export interface SearchHit {
  kind: SearchKind;
  /** catalog number, e.g. WR 002 */
  no: string;
  title: string;
  /** the series a part belongs to */
  series: string | null;
  path: string;
  /** HTML: escaped text with the matches in <mark> */
  excerpt: string;
  sections: SearchSection[];
}

/** idle: nothing loaded yet; unavailable: no index (e.g. `npm start`, see `npm run search:dev`). */
export type SearchState = 'idle' | 'loading' | 'ready' | 'unavailable';
