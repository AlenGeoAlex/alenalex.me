import { DOCUMENT, Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { SITE } from '@core/constants/site.constants';

export interface PageMeta {
  title: string;
  description: string;
  path: string;
  image?: string | null;
  type?: 'website' | 'article';
  published?: string;
  /** keep the page out of search results (drafts) */
  noindex?: boolean;
}

/** Per-page <title>, description, canonical and Open Graph tags. Written at prerender, so scrapers see them. */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly doc = inject(DOCUMENT);

  set(page: PageMeta): void {
    const url = SITE.url + page.path;
    const image = page.image ?? SITE.defaultOgImage;
    this.title.setTitle(page.title);

    const tags: Record<string, string> = {
      description: page.description,
      'og:title': page.title,
      'og:description': page.description,
      'og:url': url,
      'og:type': page.type ?? 'website',
      'og:image': image,
      'og:site_name': SITE.name,
      'twitter:card': 'summary_large_image',
      'twitter:title': page.title,
      'twitter:description': page.description,
      'twitter:image': image,
    };
    if (page.published) tags['article:published_time'] = page.published;
    tags['robots'] = page.noindex ? 'noindex, nofollow' : 'index, follow';

    for (const [key, content] of Object.entries(tags)) {
      const attr = key.startsWith('og:') || key.startsWith('article:') ? 'property' : 'name';
      this.meta.updateTag({ [attr]: key, content });
    }
    this.setCanonical(url);
  }

  private setCanonical(url: string): void {
    let link = this.doc.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) {
      link = this.doc.createElement('link');
      link.rel = 'canonical';
      this.doc.head.appendChild(link);
    }
    link.href = url;
  }
}
