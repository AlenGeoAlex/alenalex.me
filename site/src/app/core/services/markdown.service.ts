import { Injectable } from '@angular/core';
import { PostHeading } from '@core/models/post.model';

export interface RenderedMarkdown {
  html: string;
  headings: PostHeading[];
}

/**
 * Renders markdown in the browser. Only used for previews and drafts fetched live from GitHub;
 * published posts are rendered at build time by tools/build-content.mjs.
 */
@Injectable({ providedIn: 'root' })
export class MarkdownService {
  async render(markdown: string, resolveAsset: (ref: string) => string): Promise<RenderedMarkdown> {
    const { Marked } = await import('marked');
    const headings: PostHeading[] = [];
    const slugify = (s: string) =>
      s.toLowerCase().replace(/<[^>]+>/g, '').replace(/[^\w\s-]/g, '').trim().replace(/[\s_-]+/g, '-');

    const marked = new Marked({
      gfm: true,
      renderer: {
        heading({ tokens, depth }) {
          const text = this.parser.parseInline(tokens);
          const plain = new DOMParser().parseFromString(text, 'text/html').body.textContent ?? '';
          const id = slugify(plain);
          if (depth <= 3) headings.push({ id, depth, text: plain });
          return `<h${depth} id="${id}">${text}</h${depth}>`;
        },
        image({ href, text }) {
          const caption = text ? `<figcaption>${text}</figcaption>` : '';
          return `<figure class="figure"><img src="${resolveAsset(href)}" alt="${text ?? ''}" loading="lazy">${caption}</figure>`;
        },
        code({ text, lang }) {
          const language = (lang ?? '').split(/\s/)[0];
          const escaped = text.replace(/[&<>]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' })[c]!);
          if (language === 'mermaid' || language === 'mmd') return `<figure class="diagram"><pre class="mermaid">${escaped}</pre></figure>`;
          const label = language ? `<span class="code-lang">${language}</span>` : '';
          return `<figure class="code">${label}<pre class="shiki"><code>${escaped}</code></pre></figure>`;
        },
      },
    });
    return { html: await marked.parse(markdown), headings };
  }
}
