# site

The alenalex.me frontend: Angular 22 (standalone components, signals), Tailwind v4, built as static
prerendered pages for Azure Static Web Apps.

## Commands

```sh
npm install
npm start          # dev server on http://localhost:4200 (builds the blog content first)
npm run build      # production build into dist/site/browser, plus the search index
npm run content    # only rebuild the blog content from ../blogs
```

The live parts (guestbook, GitHub plot, status) talk to the API. In development that's
`http://localhost:8080` (see `src/environments/`); start it from `../api`. Without it those panels
show their offline state and everything else still works.

## Blog content

`tools/build-content.mjs` runs before every `start` and `build`. It reads `../blogs`, renders the
markdown (code highlighted with Shiki in light and dark), and writes:

- `src/content/*.json`: the post, draft and series indexes bundled into the app
- `public/content/posts/<path>.json`: one file per post with its HTML
- `public/feed.xml`: RSS

External links in a post are numbered into a references card at the end of the page (plus any
`references:` from `.meta`), and code blocks get a copy button.

Downloads listed under `files:` in `.meta` get a files card. Files in `assets/` are hashed (SHA-256) at
build time, and the download button fetches the file from R2, hashes it in the browser and only saves it
if it matches. That needs CORS on the R2 bucket (Cloudflare dashboard → R2 → bucket → Settings → CORS):

```json
[{ "AllowedOrigins": ["https://alenalex.me", "http://localhost:4200"], "AllowedMethods": ["GET", "HEAD"], "AllowedHeaders": ["*"], "MaxAgeSeconds": 86400 }]
```

Without it the button falls back to direct links plus the hash to check by hand. Big files belong in a
GitHub Release (`url:` + `sha256:`), shown as a plain link.

These are generated and gitignored. Posts, drafts and series pages are prerendered from them, so each
one has its own `<title>` and Open Graph tags. Set `BLOGS_DIR` to build from another folder.

## Search

Search runs on [Pagefind](https://pagefind.app). After `ng build`, `postbuild` indexes the prerendered
writing pages into `dist/site/browser/pagefind/`. Only elements marked `data-pagefind-body` are indexed:
published posts (title, excerpt, text) and series (title, description). Drafts never are. The site
loads `/pagefind/pagefind.js` the first time someone searches, and our own UI uses it: the
`$ grep -i` box on /writing (`?q=` in the URL) and the palette on every page (⌘K, Ctrl+K or `/`).

The index only exists in a build, so search says it's unavailable under `npm start`. To try it:

```sh
npm run search:dev   # build, index, and serve on http://localhost:1414
```

## Layout

```
src/app/
  core/       constants, models, services (API clients, content, clock, seo), tokens
  shared/     reusable components (pulsar plot, particle globe, offline notice, tag chip…) and pipes
  layout/     index rail, status bar
  features/   home, writing (index, post, series), work, guestbook, now, not-found
```

Imports use the path aliases from `tsconfig.json` (`@core`, `@shared`, `@features`, `@layout`, `@env`, `@content`).

## Section objects

The 3D icons in `public/objects/` are rendered with three.js by `tools/objects/render.html`:

```sh
npm run objects    # open http://localhost:4319/, the PNGs land in public/objects/
```

The guestbook reactions are rendered the same way into `public/objects/reactions/`: open
http://localhost:4319/?set=reactions instead. The list of reactions is in
`src/app/core/constants/reactions.constants.ts` (and `GuestbookReactions` in the API).

The site uses the `.webp` versions (convert with `cwebp -q 85`, then delete the PNGs).
