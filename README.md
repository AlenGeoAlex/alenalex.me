# alenalex.me

Source for [alenalex.me](https://alenalex.me): a personal site and blog. The repo is also the CMS;
posts are folders of markdown under `blogs/`.

## Layout

```
site/        Angular site; renders blogs/ at build time and prerenders every page
api/         .NET 10 NativeAOT API: guestbook, GitHub/Discord/homelab dashboard data
generator/   .NET CLI that validates posts and uploads their assets to Cloudflare R2
blogs/       post content
tools/       new-post.cs, a scaffolding script for posts and series
```

Each folder has its own README with the details.

## Posts

```
blogs/<folder>/
  .meta          title, date, tags, published, ... (YAML)
  index.md
  assets/        referenced as assets/x or ./assets/x

blogs/<series>/
  series.meta
  assets/
  01-intro/      a part: .meta, index.md, assets/
  02-.../
```

A folder without `.meta` (or `series.meta`) is not ready and is skipped. Posts end up at
`/writing/<slug>`, series parts at `/writing/<series-slug>/<part-slug>`. Assets are served from
`https://assets.alenalex.me/assets/hotlink-ok/<folder>/...`. The full rules for `.meta`, series and
assets are in [generator/README.md](generator/README.md).

To write a post:

```bash
dotnet run tools/new-post.cs                    # scaffold a post, series or part
cd site && npm start                            # preview at http://localhost:4200
cd generator && dotnet run --project src/AlenAlex.Generator -- validate
```

## Local development

| Part | Commands | Docs |
|---|---|---|
| Site | `cd site && npm ci && npm start` | [site/README.md](site/README.md) |
| API | `cd api/src/AlenAlex.Api && dotnet run` | [api/README.md](api/README.md) |
| Generator | `cd generator && dotnet build && dotnet test` | [generator/README.md](generator/README.md) |

## CI/CD

- **site.yml**: on changes to `site/`, validates the posts, builds the site and deploys it to Azure
  Static Web Apps.
- **blog.yml**: on changes to `blogs/` or `generator/`, tests the generator, validates the posts,
  syncs changed posts' assets to R2, then calls `site.yml`, so a page never goes live before its images.
- **api.yml**: on changes to `api/`, runs the tests and builds the Docker image; pushes from `main` go
  to `ghcr.io/alengeoalex/alenalex-api`.
