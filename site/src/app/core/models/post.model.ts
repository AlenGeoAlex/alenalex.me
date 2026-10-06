/** One entry of src/content/posts.json, built from blogs/ by tools/build-content.mjs. */
export interface PostSummary {
  no: number;
  title: string;
  pageTitle: string | null;
  date: string;
  tags: string[];
  excerpt: string | null;
  slug: string;
  /** URL under /writing/: the slug, or "<series-slug>/<part-slug>" for a part of a series */
  path: string;
  /** folder under blogs/ in the repo ("<folder>" or "<series>/<part>"); used for history and asset keys */
  folder: string;
  ogImage: string | null;
  readingMinutes: number;
  /** `ai-assist:` from .meta: whether AI helped write it; null when not stated */
  aiAssist: boolean | null;
  /** `revisions-since:` from .meta (YYYY-MM-DD): the revisions list starts at this day */
  revisionsSince: string | null;
  series: SeriesRef | null;
  /** `part:` from .meta, if set */
  part: number | null;
  /** 1-based position among the series' published parts (null for drafts and standalone posts) */
  partIndex: number | null;
  /** number of published parts in its series */
  partCount: number | null;
}

export interface SeriesRef {
  slug: string;
  title: string;
  folder: string;
}

/** One entry of src/content/series.json. */
export interface SeriesSummary {
  /** SR number; 0 for a draft series */
  no: number;
  slug: string;
  folder: string;
  title: string;
  description: string | null;
  date: string;
  /** date of the newest published part */
  updated: string;
  tags: string[];
  draft: boolean;
  cover: string | null;
  /** published parts, in reading order */
  partSlugs: string[];
  partCount: number;
}

export interface PostHeading {
  id: string;
  depth: number;
  text: string;
}

/** A source of the post: an external link in the text, or one listed under `references:` in .meta. */
export interface PostReference {
  title: string;
  url: string;
  domain: string;
}

export interface Post extends PostSummary {
  /** published: false. Readable by URL, never listed. */
  draft?: boolean;
  headings: PostHeading[];
  /** numbered in the order they first appear; the text's superscript markers point at #ref-<n> */
  references: PostReference[];
  html: string;
}

/** A post with published: false: prerendered and readable at its URL, but never listed. */
export interface DraftSummary {
  folder: string;
  slug: string;
  path: string;
  title: string;
  date: string;
  tags: string[];
}

/** One commit that touched a post. GET /api/posts/{folder}/revisions */
export interface PostRevision {
  sha: string;
  shortSha: string;
  date: string;
  message: string;
  url: string;
}

/** A post's raw source at a git ref. GET /api/posts/{folder}/source?ref= */
export interface PostSource {
  ref: string;
  meta: string;
  markdown: string;
}
