import { Injectable, inject } from '@angular/core';
import { parse as parseYaml } from 'yaml';
import { Post, PostReference, PostSummary } from '@core/models/post.model';

/** Where the previewed post sits in the site: its URL and series context (from the built index). */
/** What a preview keeps from the current version of the post (the revisions list belongs to the post, not the old version). */
export type PreviewPlacement = Pick<PostSummary, 'no' | 'path' | 'series' | 'partIndex' | 'partCount' | 'revisionsSince'>;
import { PostsApiService } from './posts-api.service';
import { MarkdownService, domainOf } from './markdown.service';

/** Renders a post as it was at any git ref (an old revision, or a draft), in the browser. */
@Injectable({ providedIn: 'root' })
export class PostPreviewService {
  private readonly api = inject(PostsApiService);
  private readonly markdown = inject(MarkdownService);

  async postAt(folder: string, ref: string, placement: PreviewPlacement): Promise<Post | null> {
    try {
      const source = await this.api.source(folder, ref);
      const meta = (parseYaml(source.meta) ?? {}) as Record<string, unknown>;
      const pageHref = `/writing/${placement.path}?preview=${encodeURIComponent(ref)}`;
      const { html, headings, references } = await this.markdown.render(source.markdown, (href) => {
        const m = /^(?:\.\/)?assets\/(.+)$/.exec(href);
        return m ? this.api.assetUrl(folder, m[1], ref) : href;
      }, pageHref);
      for (const r of metaReferences(meta['references'])) {
        if (!references.some((x) => x.url === r.url)) references.push(r);
      }
      const date = meta['date'] instanceof Date ? meta['date'].toISOString().slice(0, 10) : String(meta['date'] ?? '');
      const words = source.markdown.replace(/```[\s\S]*?```/g, '').split(/\s+/).filter(Boolean).length;
      const part = Number(meta['part']);
      return {
        ...placement,
        folder,
        part: Number.isInteger(part) && part > 0 ? part : null,
        slug: String(meta['slug'] ?? folder),
        title: String(meta['title'] ?? folder),
        pageTitle: null,
        date,
        tags: Array.isArray(meta['tags']) ? meta['tags'].map(String) : [],
        excerpt: meta['excerpt'] ? String(meta['excerpt']) : null,
        ogImage: null,
        aiAssist: typeof meta['ai-assist'] === 'boolean' ? meta['ai-assist'] : null,
        readingMinutes: Math.max(1, Math.round(words / 220)),
        headings,
        references,
        html,
      };
    } catch {
      return null;
    }
  }
}

/** `references:` from .meta; malformed entries are skipped here (the validator reports them). */
function metaReferences(value: unknown): PostReference[] {
  if (!Array.isArray(value)) return [];
  return value
    .map((r) => ({ title: String(r?.title ?? '').trim(), url: String(r?.url ?? '').trim() }))
    .filter((r) => r.title && /^https?:\/\/\S+$/.test(r.url))
    .map((r) => ({ ...r, domain: domainOf(r.url) }));
}
