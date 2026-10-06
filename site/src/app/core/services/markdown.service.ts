import { Injectable } from '@angular/core';
import { PostHeading, PostReference } from '@core/models/post.model';
import { SITE } from '@core/constants/site.constants';

/** "github.com" from "https://www.github.com/x". */
export const domainOf = (url: string) => {
  try {
    return new URL(url).hostname.replace(/^www\./, '');
  } catch {
    return url;
  }
};

export interface RenderedMarkdown {
  html: string;
  headings: PostHeading[];
  references: PostReference[];
}

/**
 * Renders markdown in the browser. Only used for previews and drafts fetched live from GitHub;
 * published posts are rendered at build time by tools/build-content.mjs.
 */
@Injectable({ providedIn: 'root' })
export class MarkdownService {
  /**
   * Same output as the build: numbered references with markers that link to `pageHref#ref-<n>`,
   * and a copy button on code blocks.
   */
  async render(markdown: string, resolveAsset: (ref: string) => string, pageHref: string): Promise<RenderedMarkdown> {
    const { Marked } = await import('marked');
    const headings: PostHeading[] = [];
    const references: PostReference[] = [];
    const escape = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);
    const slugify = (s: string) =>
      s.toLowerCase().replace(/<[^>]+>/g, '').replace(/[^\w\s-]/g, '').trim().replace(/[\s_-]+/g, '-');

    const marked = new Marked({
      gfm: true,
      renderer: {
        link({ href, title, tokens }) {
          const text = this.parser.parseInline(tokens);
          const external = /^https?:\/\//.test(href) && !href.startsWith(SITE.url);
          const t = title ? ` title="${escape(title)}"` : '';
          const rel = external ? ' target="_blank" rel="noopener"' : '';
          const link = `<a href="${escape(resolveAsset(href))}"${t}${rel}>${text}</a>`;
          if (!external) return link;
          let n = references.findIndex((r) => r.url === href) + 1;
          if (!n) {
            const plain = (new DOMParser().parseFromString(text, 'text/html').body.textContent ?? '').trim();
            references.push({ title: plain && plain !== href ? plain : href.replace(/^https?:\/\//, ''), url: href, domain: domainOf(href) });
            n = references.length;
          }
          return `${link}<sup class="ref-mark"><a href="${escape(pageHref)}#ref-${n}" aria-label="reference ${n}">${n}</a></sup>`;
        },
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
          const copy = '<button type="button" class="code-copy" aria-label="Copy code">copy</button>';
          return `<figure class="code">${label}${copy}<pre class="shiki"><code>${escaped}</code></pre></figure>`;
        },
      },
    });
    return { html: await marked.parse(markdown), headings, references };
  }
}
