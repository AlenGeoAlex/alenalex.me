# site

The alenalex.me frontend: Angular 22 (standalone components, signals), Tailwind v4, built as static
prerendered pages for Azure Static Web Apps.

## Commands

```sh
npm install
npm start          # dev server on http://localhost:4200 (builds the blog content first)
npm run build      # production build into dist/site/browser
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

These are generated and gitignored. Posts, drafts and series pages are prerendered from them, so each
one has its own `<title>` and Open Graph tags. Set `BLOGS_DIR` to build from another folder.

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
