import { Injectable, inject } from '@angular/core';
import { parse as parseYaml } from 'yaml';
import { Post, PostSummary } from '@core/models/post.model';

/** Where the previewed post sits in the site: its URL and series context (from the built index). */
export type PreviewPlacement = Pick<PostSummary, 'no' | 'path' | 'series' | 'partIndex' | 'partCount'>;
import { PostsApiService } from './posts-api.service';
import { MarkdownService } from './markdown.service';

/** Renders a post as it was at any git ref (an old revision, or a draft), in the browser. */
@Injectable({ providedIn: 'root' })
export class PostPreviewService {
  private readonly api = inject(PostsApiService);
  private readonly markdown = inject(MarkdownService);

  async postAt(folder: string, ref: string, placement: PreviewPlacement): Promise<Post | null> {
    try {
      const source = await this.api.source(folder, ref);
      const meta = (parseYaml(source.meta) ?? {}) as Record<string, unknown>;
      const { html, headings } = await this.markdown.render(source.markdown, (href) => {
        const m = /^(?:\.\/)?assets\/(.+)$/.exec(href);
        return m ? this.api.assetUrl(folder, m[1], ref) : href;
      });
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
        readingMinutes: Math.max(1, Math.round(words / 220)),
        headings,
        html,
      };
    } catch {
      return null;
    }
  }
}
