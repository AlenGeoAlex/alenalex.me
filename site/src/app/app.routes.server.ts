import { RenderMode, ServerRoute } from '@angular/ssr';
import postsJson from '@content/posts.json';
import draftsJson from '@content/drafts.json';
import seriesJson from '@content/series.json';
import { DraftSummary, PostSummary, SeriesSummary } from '@core/models/post.model';

// Typed explicitly: an empty generated file (no drafts, no series yet) would otherwise import as never[].
const posts = postsJson as PostSummary[];
const drafts = draftsJson as DraftSummary[];
const series = seriesJson as SeriesSummary[];

// Everything is prerendered to static HTML (Azure Static Web Apps has no SSR).
// Posts get one page each so chat apps and search engines see real <title> and og:* tags.
// Drafts (and draft series) are prerendered too: readable by URL, just never listed.
const paths = [...posts, ...drafts].map((p) => p.path);

export const serverRoutes: ServerRoute[] = [
  {
    path: 'writing/:slug',
    renderMode: RenderMode.Prerender,
    getPrerenderParams: async () => [
      ...paths.filter((p) => !p.includes('/')).map((slug) => ({ slug })),
      ...series.map((s) => ({ slug: s.slug })),
    ],
  },
  {
    path: 'writing/:slug/:part',
    renderMode: RenderMode.Prerender,
    getPrerenderParams: async () =>
      paths.filter((p) => p.includes('/')).map((p) => {
        const [slug, part] = p.split('/');
        return { slug, part };
      }),
  },
  { path: '**', renderMode: RenderMode.Prerender },
];
