import { Injectable, TransferState, inject, makeStateKey } from '@angular/core';
import postsIndex from '@content/posts.json';
import draftsIndex from '@content/drafts.json';
import seriesIndex from '@content/series.json';
import { POST_LOADER } from '@core/tokens/post-loader.token';
import { DraftSummary, Post, PostSummary, SeriesSummary } from '@core/models/post.model';

/** One row of the writing index: a standalone post, or a whole series. */
export type WritingEntry =
  | { kind: 'post'; date: string; post: PostSummary }
  | { kind: 'series'; date: string; series: SeriesSummary; parts: PostSummary[] };

/**
 * Blog content, built from blogs/ by tools/build-content.mjs.
 * Posts are addressed by their URL path: "<slug>", or "<series-slug>/<part-slug>" for a part.
 */
@Injectable({ providedIn: 'root' })
export class ContentService {
  /** Published posts and parts, newest first. */
  readonly posts: readonly PostSummary[] = postsIndex as PostSummary[];
  /** Unpublished posts and parts: readable by URL, never listed. */
  readonly drafts: readonly DraftSummary[] = draftsIndex as DraftSummary[];
  /** Every series, drafts included (filter on `draft` when listing). */
  readonly series: readonly SeriesSummary[] = seriesIndex as SeriesSummary[];

  private readonly loader = inject(POST_LOADER);
  private readonly transfer = inject(TransferState);

  summary(path: string): PostSummary | undefined {
    return this.posts.find((p) => p.path === path);
  }

  draft(path: string): DraftSummary | undefined {
    return this.drafts.find((d) => d.path === path);
  }

  seriesBySlug(slug: string): SeriesSummary | undefined {
    return this.series.find((s) => s.slug === slug);
  }

  /** A series' published parts, in reading order. */
  partsOf(seriesSlug: string): PostSummary[] {
    const series = this.seriesBySlug(seriesSlug);
    if (!series) return [];
    return series.partSlugs
      .map((slug) => this.summary(`${seriesSlug}/${slug}`))
      .filter((p): p is PostSummary => !!p);
  }

  /** The writing index: standalone posts and series, newest activity first. */
  entries(): WritingEntry[] {
    const posts: WritingEntry[] = this.posts
      .filter((p) => !p.series)
      .map((post) => ({ kind: 'post', date: post.date, post }));
    const series: WritingEntry[] = this.series
      .filter((s) => !s.draft && s.partCount > 0)
      .map((s) => ({ kind: 'series', date: s.updated, series: s, parts: this.partsOf(s.slug) }));
    return [...posts, ...series].sort((a, b) => b.date.localeCompare(a.date));
  }

  /** Previous and next: within the series for a part, otherwise among standalone posts. */
  neighbours(path: string): { older: PostSummary | null; newer: PostSummary | null } {
    const current = this.summary(path);
    if (current?.series) {
      const parts = this.partsOf(current.series.slug);
      const i = parts.findIndex((p) => p.path === path);
      return { older: parts[i - 1] ?? null, newer: parts[i + 1] ?? null };
    }
    const standalone = this.posts.filter((p) => !p.series);
    const i = standalone.findIndex((p) => p.path === path);
    return {
      newer: i > 0 ? standalone[i - 1] : null,
      older: i >= 0 && i < standalone.length - 1 ? standalone[i + 1] : null,
    };
  }

  /** Prerendered pages carry the post in TransferState, so hydration never refetches it. */
  async post(path: string): Promise<Post | null> {
    const key = makeStateKey<Post | null>(`post:${path}`);
    if (this.transfer.hasKey(key)) return this.transfer.get(key, null);
    const post = await this.loader(path);
    this.transfer.set(key, post);
    return post;
  }
}
