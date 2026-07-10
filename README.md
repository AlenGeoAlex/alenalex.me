# alenalex.me

Personal portfolio and blog. Built as both a real site and a deliberate
learning project — the blog generator is written in Rust specifically to
learn Rust on something real, not a tutorial.

## Layout

```
alenalex.me/
├── apps/
│   ├── portfolio-alpine/     # frontend — vanilla HTML/CSS/Alpine.js/D3, no build step
│   ├── portfolio-backend/    # small backend — guestbook, GitHub API proxying
│   └── generator/            # Rust CLI — turns blogs/ content into the site's HTML + blog spec
│
├── blogs/
│   ├── post-template.html    # {{TOKEN}} template used by the generator
│   └── posts/
│       └── <slug>/
│           ├── .meta         # YAML frontmatter (title, date, tags, ...)
│           ├── index.md      # post body, pure markdown
│           └── assets/       # images etc. referenced as `assets/<filename>`
│
├── .githooks/                # tracked git hooks (see Setup)
└── setup.sh                  # one-time per-clone setup
```

## Setup

After cloning:

```bash
./setup.sh
```

This points git at the tracked `.githooks/` folder so the pre-commit hook
actually runs on your machine (git doesn't do this automatically — hooks
normally live in the untracked `.git/hooks/`). The hook blocks manual
commits to `apps/portfolio-alpine/writing/`, since that folder is generated
output, not source — see [Generated output](#generated-output) below.

## Frontend

`apps/portfolio-alpine/` — vanilla HTML/CSS, Alpine.js, D3.js, Prism.js, all
loaded from CDN. No bundler, no build step, deliberately. Pages:

- `index.html` — homepage (terminal boot sequence / guestbook / dashboard)
- `now.html` — current status
- `writing.html` — blog list, git-log-style feed, fetches the blog spec (see below)
- `work.html` — D3 force-directed GitHub repo graph
- `writing/<slug>/index.html` — individual posts (**generated**, see below)

## Blog content

Each post lives in `blogs/posts/<slug>/`:

- **`.meta`** — plain YAML frontmatter, no `---` delimiters. If this file is
  missing, the post is treated as not-yet-ready and is skipped entirely by
  the generator (not an error).

  ```yaml
  title: Beware of poison in the source
  page-title: Poison in the Source | Alen Alex
  date: 2024-10-24
  published: true
  icon: ⚗️
  tags: ["security", "open-source", "backend"]
  type: markdown
  excerpt: Supply chain attacks are patient. Most defences are not.
  og_image_asset: cover.png   # optional — filename inside assets/
  ```

- **`index.md`** — the post body, pure markdown, no frontmatter block.
- **`assets/`** — images and other files referenced from the body. Always
  referenced as `assets/<filename>` in markdown — no `./` prefix, no
  subfolders relied on for uniqueness. This convention is load-bearing: the
  generator matches references against this exact string shape.

## The generator (`apps/generator/`)

A Rust CLI (single binary, Cargo workspace with a `generator-core` lib crate
and a `generator-bin` binary crate) that:

1. Walks `blogs/posts/`, parses each post's `.meta` (skipping posts without one).
2. For posts affected by the current change (see `CHANGED_POST_DIRS` below):
    - Reads `index.md`, parses it into an AST (via `comrak`).
    - Walks the AST once: rewrites `assets/<filename>` image references to
      their Cloudflare R2 URL, collects which assets are actually referenced,
      and replaces any reference to a missing asset with its alt text instead
      of leaving a broken image.
    - Uploads only the referenced assets to R2 (hash-compared against what's
      already there — unchanged files are skipped, not re-uploaded).
    - Substitutes the rendered HTML and post metadata into
      `blogs/post-template.html`, writes the result to
      `apps/portfolio-alpine/writing/<slug>/index.html`.
3. Builds the blog spec (list metadata for every post, always — regardless
   of which posts changed) and uploads it to R2 as `blogspec.json`.

### Why Rust

Chosen deliberately as a learning project. The generator was originally
planned in TypeScript, briefly considered in .NET, and settled on Rust. Code
in `apps/generator/` is written by hand (not AI-generated) as a way of
actually learning the language — arena-based AST manipulation, `RefCell`
borrow rules, lifetimes, and ownership were all worked through against real
compiler errors on this codebase, not toy examples.

### Assets and R2

Post assets and the blog spec are both served from Cloudflare R2
(`assets.alenalex.me`), not GitHub's raw content service. `raw.githubusercontent.com`
was the original plan, but it's rate-limited per-IP (~5,000 req/hr, shared
across everyone hitting that IP, not just this site's traffic) — not
suitable for image-heavy pages at any real scale. R2 gives full control over
caching, CORS, and public access instead.

R2 asset keys follow `assets/hotlink-ok/<slug>/<filename>` — the
`hotlink-ok` segment exists because Cloudflare's Hotlink Protection blocks
cross-origin image requests by default (checked via the `Referer` header),
and this path is configured as an exemption.

### Change-aware runs

The generator accepts a `CHANGED_POST_DIRS` env var (comma-separated list of
slugs) to avoid unnecessary R2 calls (`HEAD`/`PUT` are billed operations,
not free) on posts that haven't changed. This is computed by the CI
workflow via `git diff`, not inside the generator itself — git already has
the relevant commits checked out in CI, so this is a shell-level concern,
not something worth an API call or a dependency for.

The blog spec (`.blogspec.json` → `blogspec.json` on R2) is always rebuilt
from *every* post on every run, regardless of `CHANGED_POST_DIRS` — it's
cheap (no network calls, no rendering) and needs to stay a complete,
accurate list at all times.

If `CHANGED_POST_DIRS` is unset entirely (e.g. running locally), the
generator processes every post — the filtering is a CI-cost optimization,
not a correctness requirement.

### Running locally

```bash
cd apps/generator
cp .env.example .env   # fill in real values
cargo run
```

See `.env.example` for the required configuration (paths, R2 credentials).

## CI

`.github/workflows/generate-blog.yml` runs the generator on pushes to `main`
that touch `blogs/posts/**`. It computes `CHANGED_POST_DIRS` via `git diff`,
runs the generator, and opens a PR with the resulting changes to
`apps/portfolio-alpine/writing/` — it does not commit directly to `main`, so
generated output always goes through review before it's live.

## Generated output

`apps/portfolio-alpine/writing/` is **entirely generated** by
`apps/generator`. Do not hand-edit files in it — the pre-commit hook (see
[Setup](#setup)) blocks commits that touch this folder to catch this before
it happens. If you need to change a post, edit its source in
`blogs/posts/<slug>/` instead.

`blogspec.json` is not committed to the repo at all — it's generated and
uploaded directly to R2 on every run, fetched by the frontend from there.