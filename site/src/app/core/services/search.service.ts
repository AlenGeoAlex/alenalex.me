import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { SearchHit, SearchKind, SearchSection, SearchState } from '@core/models/search.model';

/** The bits of Pagefind's JS API we use (https://pagefind.app/docs/api/). */
interface Pagefind {
  options(options: Record<string, unknown>): Promise<void>;
  init(): Promise<void>;
  search(query: string): Promise<{ results: { id: string; data(): Promise<PagefindData> }[] }>;
}

interface PagefindData {
  url: string;
  excerpt: string;
  meta: Record<string, string | undefined>;
  sub_results?: { title: string; url: string; excerpt: string }[];
}

/** Written next to the site by `pagefind` after `ng build` (package.json postbuild). */
const PAGEFIND_URL = '/pagefind/pagefind.js';

/**
 * Search over published posts and series, backed by the Pagefind index built with the site.
 * Pagefind is only downloaded the first time someone searches, and only the parts of the index
 * for the words typed are fetched. Kept for the rest of the visit.
 */
@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private pagefind: Promise<Pagefind | null> | null = null;
  private latest = 0;

  readonly state = signal<SearchState>('idle');

  /** Hits for `query`, best first. Resolves to null when a newer search has started meanwhile. */
  async search(query: string, limit = 8): Promise<SearchHit[] | null> {
    const q = query.trim();
    const ticket = ++this.latest;
    if (!q) return [];
    const pagefind = await this.load();
    if (!pagefind || ticket !== this.latest) return pagefind ? null : [];

    const { results } = await pagefind.search(q);
    const data = await Promise.all(results.slice(0, limit).map((r) => r.data()));
    return ticket === this.latest ? data.map(toHit) : null;
  }

  /** Starts downloading Pagefind early, e.g. when a search box gets focus. */
  warmUp(): void {
    void this.load();
  }

  private load(): Promise<Pagefind | null> {
    if (!this.browser) return Promise.resolve(null);
    this.pagefind ??= (async () => {
      this.state.set('loading');
      try {
        // a runtime import: the file only exists in the built site, never in the bundle
        const url = PAGEFIND_URL;
        const pagefind = (await import(/* @vite-ignore */ url)) as Pagefind;
        await pagefind.options({ excerptLength: 22 });
        await pagefind.init();
        this.state.set('ready');
        return pagefind;
      } catch {
        this.state.set('unavailable');
        return null;
      }
    })();
    return this.pagefind;
  }
}

/** "/writing/x/#drafts" → path "/writing/x", fragment "drafts". */
function route(url: string): { path: string; fragment: string | null } {
  const [path, fragment] = url.split('#');
  return { path: path.replace(/\/(index\.html)?$/, '') || '/', fragment: fragment || null };
}

function toHit(d: PagefindData): SearchHit {
  const page = route(d.url);
  const sections: SearchSection[] = (d.sub_results ?? [])
    .map((s) => ({ ...route(s.url), title: s.title, excerpt: s.excerpt }))
    .filter((s) => s.fragment)
    .slice(0, 2);
  return {
    kind: (d.meta['kind'] as SearchKind) ?? 'post',
    no: d.meta['no'] ?? '',
    title: d.meta['title'] ?? page.path,
    series: d.meta['series'] ?? null,
    path: page.path,
    excerpt: d.excerpt,
    sections,
  };
}

/** Escapes `text` and wraps the words of `query` in <mark>, for titles (Pagefind marks excerpts itself). */
export function markMatches(text: string, query: string): string {
  const escaped = text.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);
  const words = query.trim().split(/\s+/).filter((w) => w.length > 1).map((w) => w.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
  return words.length ? escaped.replace(new RegExp(`(${words.join('|')})`, 'gi'), '<mark>$1</mark>') : escaped;
}
