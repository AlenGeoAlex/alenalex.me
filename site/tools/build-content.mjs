// Builds the site's content from blogs/. Runs before every build.
//
// blogs/ holds two kinds of top-level folders:
//   blogs/<folder>/.meta + index.md + assets/          a standalone post   → /writing/<slug>
//   blogs/<series>/series.meta (+ assets/)             a series            → /writing/<series-slug>
//   blogs/<series>/<part>/.meta + index.md + assets/   a part of a series  → /writing/<series-slug>/<part-slug>
//
// Writes:
//   src/content/posts.json                 published posts and parts, newest first (bundled)
//   src/content/series.json                every series with its parts in reading order (bundled)
//   src/content/drafts.json                draft posts/parts: rendered and reachable by URL, never listed
//   public/content/posts/<path>.json       one file per post/part with its HTML (<path> is the URL path)
//   public/feed.xml                        RSS
//
// A folder without `.meta` (or `series.meta`) isn't ready yet and is skipped.
// `published: false` makes a draft. A draft series makes all of its parts drafts, and parts are
// published in reading order (a published part after a draft part fails the build).
//
// Parts are ordered by `part:` in their .meta; parts without one come after, by date.
//
// Assets: `assets/<file>` (or `./assets/<file>`) is rewritten to the R2 public URL
//   https://assets.alenalex.me/assets/hotlink-ok/<folder path>/<file>
// where <folder path> is `<folder>` for posts and `<series>/<part>` for parts (where the generator uploads them).
// The `hotlink-ok` segment is a Cloudflare hotlink-protection exemption; keep it.
//
// BLOGS_DIR overrides the blogs/ location.

import { readdir, readFile, writeFile, mkdir, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parse as parseYaml } from 'yaml';
import { Marked } from 'marked';
import { createHighlighter } from 'shiki';

const here = dirname(fileURLToPath(import.meta.url));
const siteRoot = join(here, '..');
const blogsRoot = process.env.BLOGS_DIR ? resolve(process.env.BLOGS_DIR) : join(siteRoot, '..', 'blogs');
const SITE_URL = 'https://alenalex.me';
const ASSET_BASE = 'https://assets.alenalex.me/assets/hotlink-ok';

const slugify = (s) =>
  s.toLowerCase().normalize('NFKD').replace(/[^\w\s-]/g, '').trim().replace(/[\s_-]+/g, '-');

/** Undoes the escaping marked applies to text, for heading text that's shown as plain text. */
const decodeEntities = (s) =>
  s.replace(/&(amp|lt|gt|quot|#39);/g, (_, e) => ({ amp: '&', lt: '<', gt: '>', quot: '"', '#39': "'" })[e]);

const escapeXml = (s) =>
  s.replace(/[<>&'"]/g, (c) => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;', "'": '&apos;', '"': '&quot;' })[c]);

const listDirs = async (dir) =>
  existsSync(dir) ? (await readdir(dir, { withFileTypes: true })).filter((d) => d.isDirectory()).map((d) => d.name) : [];

/** Fields shared by .meta and series.meta. */
function parseCommon(meta, file) {
  if (!meta.title) throw new Error(`blogs/${file} is missing "title"`);
  const date = meta.date instanceof Date ? meta.date.toISOString().slice(0, 10) : String(meta.date ?? '');
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) throw new Error(`blogs/${file} has a bad "date": ${meta.date}`);
  return {
    title: String(meta.title),
    date,
    published: meta.published !== false,
    tags: Array.isArray(meta.tags) ? meta.tags.map(String) : [],
    slug: meta.slug ? String(meta.slug) : slugify(String(meta.title)),
  };
}

function parseOptionalDate(value, key, file) {
  if (value === undefined || value === null || value === '') return null;
  const date = value instanceof Date ? value.toISOString().slice(0, 10) : String(value);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) throw new Error(`blogs/${file} has a bad "${key}": ${value}`);
  return date;
}

/** `.meta` is YAML (`key: value`, arrays as JSON). */
function parseMeta(raw, file) {
  const meta = parseYaml(raw) ?? {};
  const part = meta.part === undefined || meta.part === null ? null : Number(meta.part);
  if (part !== null && !(Number.isInteger(part) && part > 0)) throw new Error(`blogs/${file} has a bad "part": ${meta.part}`);
  return {
    ...parseCommon(meta, file),
    pageTitle: meta['page-title'] ? String(meta['page-title']) : null,
    excerpt: meta.excerpt ? String(meta.excerpt) : null,
    ogImageAsset: meta.og_image_asset ? String(meta.og_image_asset) : null,
    // whether AI helped write it; null when the post doesn't say
    aiAssist: typeof meta['ai-assist'] === 'boolean' ? meta['ai-assist'] : null,
    // the revisions list starts at this day (earlier commits are drafting noise)
    revisionsSince: parseOptionalDate(meta['revisions-since'], 'revisions-since', file),
    part,
  };
}

function parseSeriesMeta(raw, file) {
  const meta = parseYaml(raw) ?? {};
  return {
    ...parseCommon(meta, file),
    description: meta.description ? String(meta.description) : null,
    coverAsset: meta.cover_asset ? String(meta.cover_asset) : null,
  };
}

function assetUrl(folder, ref) {
  const m = /^(?:\.\/)?assets\/(.+)$/.exec(ref);
  return m ? `${ASSET_BASE}/${folder}/${m[1]}` : ref;
}

/** First paragraph of plain text, used when `.meta` has no excerpt. */
function deriveExcerpt(md) {
  const para = new Marked().lexer(md).find((t) => t.type === 'paragraph' && !/^!\[/.test(t.text));
  if (!para) return null;
  const text = para.text.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1').replace(/[`*_]/g, '').replace(/\s+/g, ' ');
  return text.length > 180 ? text.slice(0, 177).trimEnd() + '…' : text;
}

/** Renders one post folder (standalone or part) to its full JSON. `folder` is the path under blogs/. */
async function renderPost(dir, folder, meta, render) {
  const md = await readFile(join(dir, 'index.md'), 'utf8');
  const { html, headings } = await render(md, folder);
  const words = md.replace(/```[\s\S]*?```/g, '').split(/\s+/).filter(Boolean).length;
  const { ogImageAsset, ...rest } = meta;
  return {
    ...rest,
    folder,
    excerpt: meta.excerpt ?? deriveExcerpt(md),
    ogImage: ogImageAsset ? assetUrl(folder, `assets/${ogImageAsset.replace(/^(\.\/)?assets\//, '')}`) : null,
    readingMinutes: Math.max(1, Math.round(words / 220)),
    headings,
    html,
  };
}

/** Reading order: numbered parts first (by number), then the rest by date. */
const byPartOrder = (a, b) =>
  (a.part ?? Infinity) - (b.part ?? Infinity) || a.date.localeCompare(b.date) || a.slug.localeCompare(b.slug);

const MERMAID_LANGS = new Set(['mermaid', 'mmd']);

async function main() {
  const highlighter = await createHighlighter({
    themes: ['github-dark-default', 'github-light'],
    langs: ['sql', 'rust', 'typescript', 'javascript', 'json', 'bash', 'csharp', 'java', 'yaml', 'toml', 'html', 'css'],
  });
  const loaded = new Set(highlighter.getLoadedLanguages());

  /** Image and link paths are resolved against the post's folder. */
  async function render(md, folder) {
    const headings = [];
    const marked = new Marked({
      gfm: true,
      renderer: {
        code({ text, lang }) {
          const language = (lang ?? '').split(/\s/)[0].toLowerCase();
          // drawn in the browser by DiagramService; the source shows as-is until then
          if (MERMAID_LANGS.has(language)) return `<figure class="diagram"><pre class="mermaid">${escapeXml(text)}</pre></figure>`;
          // both themes are emitted as CSS variables; the reader's light/dark toggle picks one
          const html = highlighter.codeToHtml(text, {
            lang: loaded.has(language) ? language : 'text',
            themes: { dark: 'github-dark-default', light: 'github-light' },
            defaultColor: false,
          });
          const label = language ? `<span class="code-lang">${escapeXml(language)}</span>` : '';
          return `<figure class="code">${label}${html}</figure>`;
        },
        image({ href, title, text }) {
          const src = assetUrl(folder, href);
          const t = title ? ` title="${escapeXml(title)}"` : '';
          const cap = text ? `<figcaption>${escapeXml(text)}</figcaption>` : '';
          return `<figure class="figure"><img src="${src}" alt="${escapeXml(text ?? '')}"${t} loading="lazy" decoding="async">${cap}</figure>`;
        },
        heading({ tokens, depth }) {
          const text = this.parser.parseInline(tokens);
          const plain = decodeEntities(text.replace(/<[^>]+>/g, ''));
          const id = slugify(plain);
          if (depth <= 3) headings.push({ id, depth, text: plain });
          return `<h${depth} id="${id}">${text}</h${depth}>`;
        },
        link({ href, title, tokens }) {
          const text = this.parser.parseInline(tokens);
          const external = /^https?:\/\//.test(href) && !href.startsWith(SITE_URL);
          const t = title ? ` title="${escapeXml(title)}"` : '';
          const rel = external ? ' target="_blank" rel="noopener"' : '';
          return `<a href="${assetUrl(folder, href)}"${t}${rel}>${text}</a>`;
        },
      },
    });
    return { html: await marked.parse(md), headings };
  }

  const all = []; // every post and part, drafts included
  const series = [];

  for (const top of await listDirs(blogsRoot)) {
    const dir = join(blogsRoot, top);
    const hasMeta = existsSync(join(dir, '.meta'));
    const hasSeries = existsSync(join(dir, 'series.meta'));
    if (hasMeta && hasSeries) throw new Error(`blogs/${top} has both .meta and series.meta; a folder is a post or a series, not both`);

    if (hasMeta) {
      if (!existsSync(join(dir, 'index.md'))) continue;
      const meta = parseMeta(await readFile(join(dir, '.meta'), 'utf8'), `${top}/.meta`);
      const post = await renderPost(dir, top, meta, render);
      all.push({ ...post, path: post.slug, draft: !post.published, series: null, partIndex: null, partCount: null });
      continue;
    }

    if (!hasSeries) continue;
    const sMeta = parseSeriesMeta(await readFile(join(dir, 'series.meta'), 'utf8'), `${top}/series.meta`);
    const parts = [];
    for (const sub of await listDirs(dir)) {
      const partDir = join(dir, sub);
      if (sub === 'assets' || !existsSync(join(partDir, '.meta')) || !existsSync(join(partDir, 'index.md'))) continue;
      const folder = `${top}/${sub}`;
      const meta = parseMeta(await readFile(join(partDir, '.meta'), 'utf8'), `${folder}/.meta`);
      parts.push(await renderPost(partDir, folder, meta, render));
    }
    parts.sort(byPartOrder);

    const partSlugs = new Set();
    for (const p of parts) {
      if (partSlugs.has(p.slug)) throw new Error(`series "${sMeta.slug}" has two parts with slug "${p.slug}"`);
      partSlugs.add(p.slug);
    }

    const seriesDraft = !sMeta.published;

    // Publish in reading order: a part can't be live while an earlier part is still a draft.
    if (!seriesDraft) {
      const firstDraft = parts.find((p) => !p.published);
      const jumped = firstDraft && parts.slice(parts.indexOf(firstDraft) + 1).find((p) => p.published);
      if (jumped) {
        throw new Error(
          `blogs/${jumped.folder}/.meta is published, but blogs/${firstDraft.folder} (an earlier part of "${sMeta.title}") ` +
            'is still a draft; publish parts in order',
        );
      }
    }
    const visible = seriesDraft ? [] : parts.filter((p) => p.published);
    const ref = { slug: sMeta.slug, title: sMeta.title, folder: top };
    for (const p of parts) {
      const draft = seriesDraft || !p.published;
      const index = visible.indexOf(p);
      all.push({
        ...p,
        path: `${sMeta.slug}/${p.slug}`,
        draft,
        series: ref,
        partIndex: index >= 0 ? index + 1 : null,
        partCount: visible.length,
      });
    }

    series.push({
      slug: sMeta.slug,
      folder: top,
      title: sMeta.title,
      description: sMeta.description,
      date: sMeta.date,
      tags: sMeta.tags,
      draft: seriesDraft,
      cover: sMeta.coverAsset ? assetUrl(top, `assets/${sMeta.coverAsset.replace(/^(\.\/)?assets\//, '')}`) : null,
      partSlugs: visible.map((p) => p.slug), // reading order, published parts only
    });
  }

  // series and post slugs share /writing/, so they can't collide
  const topLevel = new Map();
  for (const name of [...all.filter((p) => !p.series).map((p) => p.slug), ...series.map((s) => s.slug)]) {
    if (topLevel.has(name)) throw new Error(`"${name}" is used by more than one post or series; slugs must be unique`);
    topLevel.set(name, true);
  }

  // Catalog numbers follow publication order: WR for posts and parts, SR for series.
  const published = all.filter((p) => !p.draft);
  published.sort((a, b) => a.date.localeCompare(b.date) || a.path.localeCompare(b.path));
  published.forEach((p, i) => (p.no = i + 1));
  all.filter((p) => p.draft).forEach((p) => (p.no = 0));
  const publicSeries = series.filter((s) => !s.draft);
  publicSeries.sort((a, b) => a.date.localeCompare(b.date) || a.slug.localeCompare(b.slug));
  publicSeries.forEach((s, i) => (s.no = i + 1));
  series.filter((s) => s.draft).forEach((s) => (s.no = 0));

  // A series is "updated" when its newest published part is; listings sort by that.
  for (const s of series) {
    const dates = published.filter((p) => p.series?.slug === s.slug).map((p) => p.date);
    s.updated = dates.sort().at(-1) ?? s.date;
    s.partCount = s.partSlugs.length;
  }

  const outDir = join(siteRoot, 'public', 'content', 'posts');
  await rm(outDir, { recursive: true, force: true });
  await mkdir(outDir, { recursive: true });
  await mkdir(join(siteRoot, 'src', 'content'), { recursive: true });

  const summary = ({ html, headings, published: _p, ...rest }) => rest;
  const index = published.map(summary).reverse(); // newest first
  await writeFile(join(siteRoot, 'src', 'content', 'posts.json'), JSON.stringify(index, null, 2) + '\n');
  await writeFile(join(siteRoot, 'src', 'content', 'series.json'), JSON.stringify(series, null, 2) + '\n');
  // just enough to find a draft by its URL
  const draftIndex = all.filter((p) => p.draft).map(({ folder, slug, path, title, date, tags }) => ({ folder, slug, path, title, date, tags }));
  await writeFile(join(siteRoot, 'src', 'content', 'drafts.json'), JSON.stringify(draftIndex, null, 2) + '\n');

  for (const { published: _p, ...post } of all) {
    const file = join(outDir, `${post.path}.json`);
    await mkdir(dirname(file), { recursive: true });
    await writeFile(file, JSON.stringify(post));
  }

  const items = index
    .map(
      (p) => `    <item>
      <title>${escapeXml(p.series ? `${p.series.title} · ${p.title}` : p.title)}</title>
      <link>${SITE_URL}/writing/${p.path}</link>
      <guid>${SITE_URL}/writing/${p.path}</guid>
      <pubDate>${new Date(p.date + 'T00:00:00Z').toUTCString()}</pubDate>
      ${p.excerpt ? `<description>${escapeXml(p.excerpt)}</description>` : ''}
    </item>`,
    )
    .join('\n');
  await writeFile(
    join(siteRoot, 'public', 'feed.xml'),
    `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <title>Alen Alex · writing</title>
    <link>${SITE_URL}/writing</link>
    <description>I build backend systems and write about whatever I'm learning.</description>
${items}
  </channel>
</rss>
`,
  );

  console.log(
    `content: ${published.length} published, ${all.length - published.length} draft(s), ${publicSeries.length} series` +
      (series.length > publicSeries.length ? ` (+${series.length - publicSeries.length} draft series)` : ''),
  );
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
