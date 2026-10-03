# alenalex.me generator

A .NET 10 console app that validates the posts in `blogs/` and uploads their
images to Cloudflare R2.

The repo is the CMS. Each post is a folder under `blogs/`:

```
blogs/<folder>/
  .meta        title, date, tags, ... (YAML-ish; see below)
  index.md     the post
  assets/      images and other files referenced as assets/x or ./assets/x
```

A folder **without `.meta` is "not ready"** and is skipped silently everywhere. A folder with
`series.meta` instead is a **series** of posts; see [Series](#series).

Markdown is rendered by the Angular site at build time (`site/tools/build-content.mjs`), which
rewrites `assets/x` to `https://assets.alenalex.me/assets/hotlink-ok/<folder>/x`. This tool
validates every post and uploads the assets to exactly those keys.

## Commands

```bash
cd generator

# Validate every post folder; exit code 1 on any error.
dotnet run --project src/AlenAlex.Generator -- validate

# Validate, then upload assets of all ready posts (or only the changed ones) to R2.
dotnet run --project src/AlenAlex.Generator -- sync
dotnet run --project src/AlenAlex.Generator -- sync --changed test-blog,another-post
dotnet run --project src/AlenAlex.Generator -- sync --dry-run    # read-only: HEAD requests only
dotnet run --project src/AlenAlex.Generator -- sync --offline    # no network at all

# Global option on every command:
#   --posts <dir>   path to blogs/ (default: the Posts:Directory setting, else the nearest blogs/ above the cwd)
```

### `validate`

For every folder with a `.meta` (drafts included, shown as `draft`):

| Check | Severity |
|---|---|
| `.meta` parses (and would parse in the site's YAML parser: e.g. an unquoted value containing `: ` or a duplicate key is an error) | error |
| `title` present; `date` is a real `YYYY-MM-DD` date | error |
| `tags`, if present, is a list of strings; `published` and `ai-assist` are `true`/`false`; `type` is `markdown`/`html`; `slug`, if set, is `lower-case-with-dashes` | error |
| `index.md` exists | error |
| every `assets/x` / `./assets/x` in markdown images, inline links and reference definitions exists in `assets/` (exact case: R2 and Linux are case-sensitive; code blocks are ignored) | error |
| `og_image_asset`, if set, exists in `assets/` | error |
| slugs (`slug:` or the slugified title) are unique across **published** posts | error |
| unknown `.meta` keys; files in `assets/` that nothing references | warning |

Output is one line per post, then compiler-style diagnostics (`blogs/x/.meta:3: error: ...`).
In GitHub Actions they are also emitted as `::error file=...,line=...::` annotations.

Slugs follow the site's rule (see [Slugs](#slugs)).

### `sync`

1. Runs `validate`; any error aborts before anything is uploaded.
2. Picks post folders: `--changed`, else the `Posts:Changed` setting (comma list; `*` or empty = all).
   Names of deleted/renamed folders are reported as `skip` (not an error), as are folders without `.meta`.
3. For every file in each selected `assets/` (recursively, hidden files like `.DS_Store` ignored)
   uploads to

   ```
   assets/hotlink-ok/<folder>/<path inside assets/>
   ```

   e.g. `assets/hotlink-ok/test-blog/rustlogo.png`. `hotlink-ok` is a Cloudflare hotlink-protection
   exemption: do not change the key shape. Nested files keep their sub-path, matching how the site
   rewrites `assets/sub/x.png`.
4. Skips unchanged files: each object carries the file's SHA-256 in `x-amz-meta-file-metadata`,
   and a `HEAD` compares it, so objects uploaded earlier are recognised. A file is re-uploaded when
   the hash differs or is missing, or when its `Content-Type` or `Cache-Control` differ.
5. `Content-Type` comes from the extension. `Cache-Control` is `public, max-age=86400` because post
   assets keep human names and may be replaced in place; only content-addressed names
   (`name.3fa9b2c1.png`, `name-3fa9b2c1d4.png`) get `public, max-age=31536000, immutable`.

One line per post and per file: `uploaded`, `updated`, `unchanged`, `would upload`, `would update`,
`would sync` (offline), `skipped`, `error`. A fatal storage error (bad credentials, unknown bucket,
unreachable endpoint) stops further requests instead of failing every file. Exit codes: `0` ok,
`1` validation or upload failures, `2` configuration problem (no `blogs/`, missing R2 env vars).

`--dry-run` never writes. With R2 env vars set it performs read-only `HEAD`s to say what would change;
without them it falls back to `--offline`, which makes no network calls and lists every asset.

## Series

A series is a top-level folder with `series.meta` (and **no** `.meta`; having both is an error).
Each subfolder that has a `.meta` is a **part**: an ordinary post folder with `.meta`, `index.md`
and `assets/`. Subfolders without `.meta` are not ready and skipped silently, like posts.

```
blogs/guestbook-rewrite/
  series.meta
  assets/cover.png                 series-level files (optional)
  01-intro/
    .meta  index.md  assets/architecture.png
  02-database/
    .meta  index.md  assets/schema.png
  03-deploy/                       no .meta yet: not ready, skipped
```

`series.meta`:

```yaml
title: Guestbook rewrite          # required
date: 2025-01-10                  # required, when the series started
published: true                   # default true; false makes the series AND all its parts drafts
description: Rebuilding the guestbook API in Rust, one piece at a time.
tags: ["rust", "axum"]
slug: guestbook-rewrite           # optional; default is the slugified title
cover_asset: cover.png            # optional; must exist in the series' own assets/
```

A part's `.meta` takes every usual post key plus an optional `part:`:

```yaml
title: Picking a database
date: 2025-01-20
part: 2                           # positive whole number
og_image_asset: schema.png
```

Rules (checked by `validate`, and by `sync` before it uploads anything):

| Rule | Severity |
|---|---|
| Parts are ordered by `part` ascending; parts without `part` come after, by date and then slug | |
| Two parts in one series with the same `part` number | error |
| A published part after a draft part (in a published series): parts go out in reading order | error |
| Gaps in the numbering (1, 2, 4) | warning |
| Some parts numbered and some not | warning |
| A series with no ready parts; series assets not used as `cover_asset` | warning |
| `cover_asset` missing from the series' `assets/` | error |
| Series slugs must be unique among published top-level posts **and** series | error |
| Part slugs must be unique within their series (the same part slug in two series is fine) | error |
| `part:` on a standalone post | warning (ignored) |

URLs: the series is `/writing/<series-slug>` and each part `/writing/<series-slug>/<part-slug>`
(part slug = its `slug:` or slugified title). `validate` prints the series with its parts indented:

```
  series        guestbook-rewrite  /writing/guestbook-rewrite  3 parts, 1 asset
    part 1        01-intro         /writing/guestbook-rewrite/intro  2 assets
    part 2        02-database      /writing/guestbook-rewrite/picking-a-database  1 asset
    part 3        03-deploy        /writing/guestbook-rewrite/deploying-it  0 assets, draft
```

R2 keys use folder names, nested:

- series files: `assets/hotlink-ok/<series-folder>/<path inside assets/>`
- part files: `assets/hotlink-ok/<series-folder>/<part-folder>/<path inside assets/>`

Changed dirs (`--changed`, `Posts:Changed`, CI) name **top-level** folders, so a change anywhere
inside a series syncs the whole series: its own assets and every part's.

## Configuration

Standard .NET configuration, in increasing priority (later wins):

1. `src/AlenAlex.Generator/appsettings.json`: committed defaults, **no secrets**.
2. `src/AlenAlex.Generator/appsettings.Development.json`: **gitignored**. Put your own R2 keys here to run `sync` locally.
3. Environment variables in the standard `Section__Key` form. CI uses these.

| Setting | Env var | Needed for | Notes |
|---|---|---|---|
| `R2:AccountId` | `R2__AccountId` | sync | endpoint `https://<id>.r2.cloudflarestorage.com`, region `auto` |
| `R2:AccessKey` / `R2:SecretKey` | `R2__AccessKey` / `R2__SecretKey` | sync | R2 API token (Object Read & Write, scoped to the bucket) |
| `R2:Bucket` | `R2__Bucket` | sync | bucket name |
| `R2:PublicUrlBase` | `R2__PublicUrlBase` | optional | printed next to uploaded files; defaults to `https://assets.alenalex.me` |
| `Posts:Directory` | `Posts__Directory` | both | path to `blogs/`, relative to the working directory; empty = find the nearest `blogs/` |
| `Posts:Changed` | `Posts__Changed` | sync | comma-separated folder names; `*`/empty = all. `--changed` overrides it |

Local example, `src/AlenAlex.Generator/appsettings.Development.json`:

```json
{
  "R2": {
    "AccountId": "<cloudflare account id>",
    "AccessKey": "<access key id>",
    "SecretKey": "<secret access key>",
    "Bucket": "<bucket>"
  }
}
```

The file is copied next to the binary at build time, so rebuild after editing it (`dotnet run` does this for you).
Missing R2 settings make `sync` exit with code 2 and name the missing keys; `--dry-run` without them falls back to `--offline`.

## CI

`.github/workflows/blog.yml` runs on pushes to `main` touching `blogs/**` or `generator/**`, and on
manual dispatch (`changed_dirs` input). It:

1. diffs the push (`before..after`) and turns `blogs/<folder>/...` paths into `Posts__Changed`;
2. builds and tests the generator;
3. runs `validate`, then `sync` with the R2 secrets;
4. calls `site.yml` to rebuild and deploy the site, so pages never go live before their images.

`site.yml` also runs `validate` before building. The tool only runs in CI or locally, so it is a
plain `dotnet run` app with no publish step.

## Library choices

- **AWSSDK.S3** for R2 (`Storage/S3ObjectStore.cs`). R2 needs: `ServiceURL = https://<account>.r2.cloudflarestorage.com`,
  `AuthenticationRegion = "auto"`, `ForcePathStyle = true`, `RequestChecksumCalculation` /
  `ResponseChecksumValidation = WHEN_REQUIRED` (newer SDKs send CRC checksums R2 rejects), and on
  PUT `DisablePayloadSigning = true`, `UseChunkEncoding = false` (R2 rejects aws-chunked payloads).
  The sync only sees `IObjectStore` (HEAD + PUT), which the tests fake.
- **YamlDotNet** for `.meta` and `series.meta` (`Posts/MetaYaml.cs`). YAML errors carry the line
  YamlDotNet reports and are reworded where possible (e.g. an unquoted value containing `: `);
  other errors point at the key's line. YamlDotNet stops at the first syntax error, so fixing one
  may reveal the next.
- **System.CommandLine 2** for the CLI (`--help`, options, exit codes).

### Slugs

`Posts/Slugifier.cs` mirrors the site's rule (`site/tools/build-content.mjs`): lowercase, strip accents
(NFKD), keep ASCII letters and digits, and collapse spaces, underscores and dashes into single dashes.
It is only used to print URLs and catch duplicate slugs early; the site build decides the real URLs.

## Layout

```
generator/
  AlenAlex.Generator.sln, global.json
  src/AlenAlex.Generator/
    Program.cs            CLI wiring (System.CommandLine)
    Cli/                  validate / sync commands, posts dir resolution, exit codes
    Posts/                PostFolder/SeriesFolder/BlogLayout, PostMeta + SeriesMeta (YamlDotNet via MetaYaml), Slugifier, asset reference scanner
    Validation/           PostValidator, Diagnostic
    Sync/                 AssetSyncService, AssetKey, ChangedDirs, ContentTypes, CachePolicy
    Storage/              IObjectStore, S3ObjectStore (AWSSDK.S3, configured for R2), R2Options
    Output/               ConsoleOutput (aligned labels, colour, GitHub annotations)
  tests/AlenAlex.Generator.Tests/   xUnit v3 (Microsoft.Testing.Platform)
```

```bash
dotnet build && dotnet test
```
