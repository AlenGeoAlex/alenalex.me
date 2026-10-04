# alenalex.me API

Backend for [alenalex.me](https://alenalex.me), written in C# on .NET 10 and published as a
single NativeAOT binary. It runs the guestbook and serves the dashboard data (GitHub stats and
contributions, Discord presence, Spotify now-playing, homelab health) and blog post history.
External data is cached in-process, so the browser never calls a third-party API.

## Layout

Vertical slices: one folder per use case, holding its request/response records, handler and
endpoint mapping.

```
AlenAlex.Api.slnx
src/AlenAlex.Api/
  Program.cs                      startup: config, DI, migrations, middleware, endpoints
  Features/
    Health/                       GET /_health
    Guestbook/
      GuestbookFeature.cs         AddGuestbook() + MapGuestbook(): one explicit line per slice
      ListEntries/                GET    /api/guestbook
      CreateEntry/                POST   /api/guestbook (+ validator, id generator)
      LikeEntry/                  POST   /api/guestbook/{id}/likes
      UnlikeEntry/                DELETE /api/guestbook/{id}/likes
      ModerateEntry/              approve / reject (driven by Discord, no HTTP endpoint)
      ListPendingEntries/         /guestbook-pending on Discord
      ReactToEntry/               Alen's reactions on accepted entries (Discord buttons, /guestbook-react)
      Shared/                     entry model, IGuestbookRepository, error filter, notifier interface
    Github/
      GetSummary/                 GET /api/github
      RefreshSummary/             background job (every 15 min) + GraphQL client
      Shared/                     summary model + store, HttpClient setup
    Posts/
      GetRevisions/               GET /api/posts/{folder}/revisions?since=, /api/posts/{series}/{part}/revisions?since=
      GetSource/                  GET /api/posts/{folder}/source?ref=, /api/posts/{series}/{part}/source?ref=
      GetAsset/                   GET /api/posts/{folder}/assets/{file}?ref=, /api/posts/{series}/{part}/assets/{file}?ref=
      Shared/                     PostPath (folder or series/part), GitHub REST content client, cache, validation, errors
    Status/
      GetStatus/                  GET /api/status
      PollHomelab/                background poller (every 60 s)
      RecordTrack/                saves each new Spotify song from the gateway; keeps the last 5
      Shared/                     live status store + models
  Infrastructure/
    Persistence/                  IUnitOfWork(+Factory), FluentMigrator migrations, DatabaseMigrator
      Postgres/                   Npgsql unit of work + repository, connection string, keep-alive
    Discord/                      NetCord gateway (presence + moderation), moderation notifier
    Http/                         IEndpoint, client IP + hashing, error bodies, exception handler
  Options/                        ApiOptions, DiscordOptions, GithubOptions, HomelabOptions
  Json/                           AppJsonContext (source-generated, camelCase)
tests/
  AlenAlex.Api.Tests/             fast unit tests, no I/O
  AlenAlex.Api.IntegrationTests/  real PostgreSQL (Testcontainers) + FluentMigrator, the real app in-process,
                                  WireMock (Testcontainers) emulating GitHub and the homelab
```

### Conventions

- A slice is `XxxEndpoint : IEndpoint` (static `MapEndpoint`), `XxxHandler` and its records,
  in `Features/<Feature>/<UseCase>/`. Slices don't call each other's handlers; code shared
  within a feature goes in `Features/<Feature>/Shared/`.
- Registration is explicit (no assembly scanning, which NativeAOT can't do): add the handler
  to the feature's `Add<Feature>()` and the endpoint to its `Map<Feature>()`.
- JSON is source-generated: every request/response type must be listed in
  `Json/AppJsonContext.cs`. The contract is camelCase; nulls are written.
- Data access goes through `IUnitOfWorkFactory` → `IUnitOfWork.Guestbook`
  (`IGuestbookRepository`); handlers don't touch Npgsql. Writes run inside
  `BeginAsync()` / `CommitAsync()`; disposing without commit rolls back.
- SQL is never built from strings. Each repository method uses a `const` query and typed
  parameters (`command.WithText("id", id)` → `NpgsqlDbType.Text`). `src/AlenAlex.Api/.editorconfig` makes CA2100
  (non-constant command text) and CA3001 (SQL injection taint analysis) build errors. The
  repository and HTTP tests push injection payloads through every parameter.
- Expected guestbook failures are exceptions (`GuestbookValidationException` → 422,
  `GuestbookRateLimitedException` → 429, `GuestbookEntryNotFoundException` → 404), mapped by
  an endpoint filter. Anything unexpected becomes `500 {"error":"internal error"}`.

## Run locally

```sh
docker run -d --name pg -e POSTGRES_PASSWORD=dev -p 5432:5432 postgres:16-alpine
cd src/AlenAlex.Api
cp appsettings.Development.example.json appsettings.Development.json
dotnet run          # http://localhost:8080, Development environment; migrates the database on start
```

`appsettings.Development.json` is gitignored. The example sets the hashing salt to `dev`, allows
CORS from `http://localhost:4200`, connects to that local PostgreSQL
(`Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=dev`), turns the
keep-alive off and enables Scalar at
<http://localhost:8080/scalar>. Without tokens, `/api/github` returns
`503 {"error":"warming up"}`, the post endpoints `503 {"error":"github not configured"}`,
`discord.status` is `"unknown"`, and new entries aren't sent to Discord (a warning is logged).

Tokens go in user secrets, which live outside the repo and are loaded in Development:

```sh
cd src/AlenAlex.Api
dotnet user-secrets set "Github:Token" "github_pat_..."
dotnet user-secrets set "Discord:BotToken" "..."
dotnet user-secrets set "Discord:GuildId" "123456789012345678"
dotnet user-secrets set "Discord:ChannelId" "123456789012345678"
dotnet user-secrets set "Discord:UserId" "123456789012345678"
dotnet user-secrets list
```

To approve an entry locally without Discord:

```sh
docker exec pg psql -U postgres -c "UPDATE guestbook_entries SET status='accepted' WHERE id='<id>'"
```

## Tests

```sh
dotnet test                                     # everything
dotnet test tests/AlenAlex.Api.Tests            # unit tests only (no Docker, < 1 s)
dotnet test tests/AlenAlex.Api.IntegrationTests # needs Docker (PostgreSQL + WireMock containers)
```

- Unit tests: validation, IP hashing (fixed vectors that stored hashes depend on), client IP
  resolution, post input validation, GitHub summary building, presence mapping, options binding,
  the database URI conversion and the keep-alive.
- Integration tests:
  - `GuestbookRepositoryContractTests` is an abstract suite against `IGuestbookRepository` /
    `IUnitOfWork`; `PostgresGuestbookRepositoryTests` runs it on PostgreSQL (`postgres:16-alpine`
    via Testcontainers; every test gets its own fresh database), plus Postgres specifics: the
    status check constraint, microsecond UTC timestamps, cascade delete, and imported rows
    keeping their `seq`.
  - Migrations: a fresh database gets the full schema; running them again is a no-op.
  - The real app via `WebApplicationFactory` on its own PostgreSQL database: the guestbook contract (exact
    error bodies, likes, rate limit), CORS, status, health and post input validation
    (including raw `..` path segments).
  - A WireMock container (Testcontainers) stands in for GitHub (GraphQL, and REST
    commits/contents including 404, rate-limit 403, binary and >10 MB assets, cache hits) and
    the homelab services (200, 500, 302, and a delay past the 5 s timeout).
  - Without Docker, the container-backed tests are skipped with a message.
  - Discord can't be emulated, so tests swap `IGuestbookModerationNotifier` for
    `FakeModerationNotifier` and check that new entries were queued.

## Configuration

Standard ASP.NET Core configuration, in increasing priority: `appsettings.json` →
`appsettings.{Environment}.json` → user secrets (Development only) → environment variables
(`Section__Key`) → command line. Options are validated on startup, so a missing
`Api:HashingSalt` or a malformed homelab entry stops the app with a clear message.

| Key | Env var (production) | Default | Purpose |
|---|---|---|---|
| `ConnectionStrings:Guestbook` | `ConnectionStrings__Guestbook` | **required, secret** | PostgreSQL: Aiven's `postgres://…?sslmode=require` URI or an Npgsql connection string |
| `Database:KeepAliveInterval` | `Database__KeepAliveInterval` | `01:00:00` | `SELECT 1` this often so a free-tier database isn't powered off; `00:00:00` = off |
| `Database:Schema` | `Database__Schema` | `public` | Schema for the guestbook tables and FluentMigrator's `VersionInfo`; queries use it as the search path |
| `Api:HashingSalt` | `Api__HashingSalt` | **required, secret** | Salt for hashing visitor IPs; must not change, see below |
| `Api:IpHeader` | `Api__IpHeader` | — | e.g. `CF-Connecting-IP`; otherwise the TCP peer address is used |
| `Api:AllowedOrigins` | `Api__AllowedOrigins__0`, `__1`, … | `[]` | CORS origins (GET/POST/DELETE/OPTIONS, `Content-Type`) |
| `Api:EnableScalar` | `Api__EnableScalar` | `false` | OpenAPI (`/openapi/v1.json`) + Scalar UI (`/scalar`) |
| `Discord:BotToken` | `Discord__BotToken` | — (secret) | Enables the bot (moderation + presence) |
| `Discord:GuildId` | `Discord__GuildId` | — | Guild for slash commands and presence |
| `Discord:ChannelId` | `Discord__ChannelId` | — | Channel for moderation embeds |
| `Discord:UserId` | `Discord__UserId` | — | Whose presence to show; the only moderator (anyone if unset) |
| `Github:Token` | `Github__Token` | — (secret) | GraphQL summary and post history (Contents: read); without it 503 |
| `Github:Login` | `Github__Login` | `AlenGeoAlex` | GitHub user for the summary |
| `Github:Repo` | `Github__Repo` | `AlenGeoAlex/alenalex.me` | Repo holding the posts |
| `Github:BlogsPath` | `Github__BlogsPath` | `blogs` | Folder of the posts in that repo |
| `Github:ApiUrl` / `Github:GraphQlUrl` | `Github__ApiUrl` / `Github__GraphQlUrl` | api.github.com | Overridden by the integration tests |
| `Homelab:Services` | `Homelab__Services__0__Name`, `Homelab__Services__0__Url`, … | `[]` | Services to health-check every 60 s (2xx/3xx within 5 s = up) |
| `ASPNETCORE_HTTP_PORTS` / `urls` | `ASPNETCORE_HTTP_PORTS` | 8080 in Docker and `dotnet run` | Listen port (standard Kestrel settings) |
| `Logging:LogLevel:*` | `Logging__LogLevel__Default`, … | see appsettings.json | Standard logging |

In JSON, the lists are real arrays:

```json
{
  "Api": { "AllowedOrigins": [ "https://alenalex.me" ], "IpHeader": "CF-Connecting-IP" },
  "Homelab": {
    "Services": [
      { "Name": "jellyfin", "Url": "http://jellyfin:8096" },
      { "Name": "nas", "Url": "https://nas.local" }
    ]
  }
}
```

**`Api:HashingSalt` must not change once data exists.** Likes, rate limits and pending-entry
ownership are keyed by `hex(sha256(salt + ip))`; a new salt or hash scheme detaches every stored
like and pending entry from its visitor.

## API

Responses are JSON with camelCase fields; errors are `{ "error": "..." }`.

| Method | Path | Notes |
|---|---|---|
| GET | `/_health` | `OK` (text/plain) |
| GET | `/api/guestbook` | `{ entries }`: accepted + the caller's own pending, newest first |
| POST | `/api/guestbook` | `{ name, message }` → `201 { entry }`; `422` invalid; `429` more than 5/hour/IP |
| POST | `/api/guestbook/{id}/likes` | `200 { likeCount, liked: true }`, idempotent; `404 {"error":"entry not found"}` unless accepted |
| DELETE | `/api/guestbook/{id}/likes` | `200 { likeCount, liked: false }`, idempotent; `404` unless accepted |
| GET | `/api/github` | Cached summary, refreshed every 15 min; `503 {"error":"warming up"}` until the first fetch |
| GET | `/api/status` | Discord presence, Spotify track, `recentTracks` (last 5 songs, newest first: `{ track, artist, album, artUrl, playedAt }`), homelab health |
| GET | `/api/posts/{folder}/revisions?since=` | `{ revisions: [{ sha, shortSha, date, message, url }] }`: commits that changed `index.md`, newest first, max 30, cached 10 min. Commits with `[skip rev]` in the message are left out; `since` (`YYYY-MM-DD`, optional) drops older ones |
| GET | `/api/posts/{folder}/source?ref=` | `{ ref, meta, markdown }` (raw `.meta` + `index.md`); `ref` defaults to `main` |
| GET | `/api/posts/{folder}/assets/{file}?ref=` | Raw image bytes (png/jpg/jpeg/gif/webp/svg/avif, max 10 MB). Also series-level assets: `/api/posts/{series}/assets/cover.png` |
| GET | `/api/posts/{series}/{part}/revisions` | Same as above, for a part of a series (`blogs/{series}/{part}`) |
| GET | `/api/posts/{series}/{part}/source?ref=` | Same, from `blogs/{series}/{part}/.meta` + `index.md` |
| GET | `/api/posts/{series}/{part}/assets/{file}?ref=` | Same, from `blogs/{series}/{part}/assets/{file}` |

Blog layout in the repo: standalone posts are `blogs/<folder>/{.meta,index.md,assets/}`; a series is
`blogs/<series>/series.meta` (+ `assets/`) with one folder per part,
`blogs/<series>/<part>/{.meta,index.md,assets/}`. Literal route segments win, so
`/api/posts/a/assets/x.png` is always the folder asset route, and a series part can't be
named `assets`.

Entry shape:

```json
{ "id": "V1StGXR8_Z5jdHi6B-myT", "seq": 42, "name": "Ada", "message": "hello",
  "status": "pending", "createdAt": "2026-10-02T20:17:01.393147+00:00",
  "likeCount": 0, "liked": false, "reactions": ["heart", "fire"] }
```

`reactions` are Alen's, set from Discord: approving an entry swaps the Approve/Reject buttons for
one toggle button per reaction, and `/guestbook-react id:<entry> [reaction:<pick>]` toggles one
(or, without `reaction`, replies with the buttons) for entries approved earlier. The list lives in
`Features/Guestbook/Shared/GuestbookReactions.cs`; to add one, add it there and in the site's
`core/constants/reactions.constants.ts` with its object in `site/public/objects/reactions/`.

Guestbook validation: `name` 1–40 characters, `message` 3–200 characters, both after trimming,
counted in Unicode characters (not bytes or UTF-16 units). Errors:
`{"error":"name must be 1–40 characters"}`, `{"error":"message must be 3–200 characters"}`,
`{"error":"too many entries, please try again later"}`.

Post endpoints are strictly validated: every path segment (`folder`, `series`,
`part`, `file`) must match
`^[A-Za-z0-9._-]{1,100}$` and not be `.`/`..`; `ref` must match `^[A-Za-z0-9._/-]{1,100}$`
without `..`; asset files must have an image extension. Otherwise `400 {"error":"bad request"}`
(checked before anything else). Then: `503 {"error":"github not configured"}` without a token;
`404 {"error":"post not found"}` (`"asset not found"` for assets) when GitHub answers 404;
`502 {"error":"github unavailable"}` for other GitHub failures (rate limit, 5xx, network);
`502 {"error":"asset too large"}` over 10 MB. Caching: content at a 40-character commit sha
is cached in memory for 24 h and sent with `Cache-Control: public, max-age=86400`; branch
refs are cached 2 min (`max-age=120`). Assets also get `X-Content-Type-Options: nosniff` and
a sandboxing `Content-Security-Policy` (SVGs can contain script).

Malformed JSON bodies get `400 {"error":"bad request"}`.

## Database and migrations

PostgreSQL via Npgsql (`NpgsqlSlimDataSourceBuilder` with only TLS enabled, to keep the AOT
binary small), hand-written SQL behind `IGuestbookRepository` / `IUnitOfWork`
(`Infrastructure/Persistence/Postgres`).

Migrations use FluentMigrator's PostgreSQL processor (`Infrastructure/Persistence/Migrations`),
applied on startup by `DatabaseMigrator` and tracked in `"VersionInfo"`. Add new migrations to
`DatabaseMigrator.All()`.

`20261004001` creates:

- `guestbook_entries(id text pk, seq bigint identity (BY DEFAULT) unique, name, message,
  status text CHECK in ('pending_approval','accepted','rejected'), ip_hash, rejection_reason,
  created_at timestamptz)`, indexed on `(status, created_at)` and `(ip_hash, created_at)`.
  `seq` is the number shown on the site (gb·0042); BY DEFAULT lets imported rows keep theirs.
- `guestbook_likes(entry_id → guestbook_entries ON DELETE CASCADE, ip_hash, created_at timestamptz,
  pk(entry_id, ip_hash))`.

Ids are 21-character nanoids. The API exposes `pending` / `accepted` and never returns rejected
entries.

### Aiven

1. Create a PostgreSQL service (the free plan is enough). In the service overview, copy the
   **Service URI** (`postgres://avnadmin:…@….aivencloud.com:12345/defaultdb?sslmode=require`).
2. Put it in `compose.env` as `ConnectionStrings__Guestbook=…` (or in user secrets for local use).
   The API converts the URI to an Npgsql connection string; `sslmode=require` means TLS without
   certificate verification, which is what Aiven's URI asks for.
3. Optional, stricter: download the service's **CA certificate** from the overview, mount it into
   the container and use `?sslmode=verify-full&sslrootcert=/path/to/ca.pem` (or
   `SSL Mode=VerifyFull;Root Certificate=/path/to/ca.pem`). `Trust Server Certificate` is never needed.
4. Free services are powered off after a period without activity. The API runs `SELECT 1` every
   `Database:KeepAliveInterval` (default 1 hour) to prevent that; failures are only logged.
   If it does get powered off, power it on in the console; the API's container restarts until
   the database is reachable (migrations run at startup).
5. Using a dedicated user instead of `avnadmin`? On PostgreSQL 15+ it can't create tables in
   `public` ("permission denied for schema public"). As `avnadmin`, give it its own schema and point
   the API at it:

   ```sql
   CREATE SCHEMA guestbook AUTHORIZATION guestbook_app;
   ```

   then set `Database__Schema=guestbook`. The tables and `VersionInfo` are created there, and the
   API's connection uses it as the search path.

### Importing the old SQLite guestbook (once)

Standard tools only: `sqlite3` and `psql` (`docker exec -i <pg-container> psql …` works too).
Start the API against the new database once first (or run it now and stop it) so the tables
exist and are empty. Then, on a **copy** of `guestbook.db`:

```sh
# Export. Use `seq` instead of `rowid AS seq` if the table already has a seq column
# (sqlite3 guestbook.db "PRAGMA table_info(guestbook_entries)").
# Old databases may hold 'pending' or '' statuses; the CASE folds them into 'pending_approval'
# here, because the Postgres table's CHECK constraint rejects anything else.
sqlite3 guestbook.db <<'SQL'
.headers on
.mode csv
.once entries.csv
SELECT id, rowid AS seq, name, message,
       CASE WHEN status IN ('pending', '') THEN 'pending_approval' ELSE status END AS status,
       ip_hash, rejection_reason, created_at
FROM guestbook_entries ORDER BY seq;
.once likes.csv
SELECT entry_id, ip_hash, created_at FROM guestbook_likes;
SQL

# Import. timezone=UTC so the oldest rows ('2026-07-11 10:00:00', no offset) are read as UTC.
PGOPTIONS='-c timezone=UTC' psql "$DATABASE_URI" -v ON_ERROR_STOP=1 <<'SQL'
\copy guestbook_entries (id, seq, name, message, status, ip_hash, rejection_reason, created_at) from 'entries.csv' with (format csv, header)
\copy guestbook_likes (entry_id, ip_hash, created_at) from 'likes.csv' with (format csv, header)
-- new entries continue after the highest imported number
SELECT setval(pg_get_serial_sequence('guestbook_entries', 'seq'), (SELECT MAX(seq) FROM guestbook_entries));
SQL
```

(`likes.csv` is empty, without even a header, when there are no likes; `\copy` then imports 0 rows.)
Keep `Api__HashingSalt` exactly as before: likes and pending-entry ownership are keyed by the
salted IP hash.

## NativeAOT

```sh
dotnet publish src/AlenAlex.Api -c Release -r osx-arm64     # or linux-x64 / linux-arm64
```

The output is `alenalex-api` (about 40 MB, a single file) plus `appsettings.json` (the Development files are never published). Run the binary from
that directory so `appsettings.json` is picked up, and set the port with
`ASPNETCORE_HTTP_PORTS=8080` (or `--urls`).
On macOS, if linking fails with `library 'dl' not found`, run
`export SDKROOT=$(xcrun --show-sdk-path)` first. On Linux the build needs `clang` and
`zlib1g-dev` (the Dockerfile installs them).

The app's own code has no trim/AOT warnings: the analyzers run on every build via
`IsAotCompatible`, and the important IL codes are errors. The only suppressed warnings come
from FluentMigrator, which isn't trim-annotated:

- FluentMigrator's assemblies have trim/AOT warnings for assembly scanning
  (`Assembly.GetExportedTypes`, `Activator`/`ActivatorUtilities` on scanned types:
  IL2026/IL2067/IL2072), its reflection-based ADO.NET provider lookup
  (`Assembly.LoadFrom`/`LoadFile`, `Assembly.GetType`, GAC probing: IL2026/IL2070/IL2072,
  and `Assembly.Location`: IL3000), and DataAnnotations validation of migration expressions
  (IL2026).
- Those scanning and provider-probing paths never run: `DatabaseMigrator` gives the runner an
  explicit migration list (`ExplicitMigrationSource`) and returns `NpgsqlFactory.Instance`
  directly (`AotPostgresDbFactory`). The published binary was checked against a fresh
  PostgreSQL database and one holding imported data.
- The csproj (`_FluentMigratorAotWarnings` target) compiles only the `FluentMigrator.*`
  assemblies in ILC's single-warn mode (one IL2104/IL3053 each) and silences IL2104, IL3053
  and IL3000 for ILC only. No other assembly is single-warn, so any other trim/AOT problem
  still shows up.

## Docker / deploy (homelab, behind api.alenalex.me)

### Docker Compose

`compose.yaml` runs the published image (`ghcr.io/alengeoalex/alenalex-api`, pushed by `.github/workflows/api.yml`;
multi-arch, so the same tag runs on amd64 and arm64 hosts, including Docker on Apple Silicon)
with the port bound to loopback only, a read-only root filesystem and all capabilities dropped,
plus `cloudflared` for the tunnel. The database is Aiven's (storage and backups are theirs).
Secrets, including the database URI, live in `compose.env` (gitignored; start from `compose.env.example`).

```sh
cp compose.env.example compose.env       # ConnectionStrings__Guestbook + Api__HashingSalt (required), tokens
docker compose up -d                     # pull + run :latest (API_TAG=sha-abc1234 to pin a build)
docker compose up -d --build             # or build from this folder instead
docker compose ps                        # shows (healthy) once /_health answers
docker compose pull && docker compose up -d   # update
```

`API_PORT` changes the host port (default 8080). If the GHCR package is private, log in once with
`docker login ghcr.io -u AlenGeoAlex` using a token with `read:packages`.

To bring the old SQLite guestbook over, see [Importing the old SQLite guestbook](#importing-the-old-sqlite-guestbook-once).

The image's `HEALTHCHECK` runs `alenalex-api --healthcheck`, which calls `/_health` and exits
0 or 1; the chiseled image has no shell or curl.

### `docker run`

```sh
docker build --platform linux/amd64 -t alenalex-api .
docker run -d --name alenalex-api --restart unless-stopped \
  -p 127.0.0.1:8080:8080 \
  -e ConnectionStrings__Guestbook='postgres://avnadmin:…@….aivencloud.com:12345/defaultdb?sslmode=require' \
  -e Api__HashingSalt='<salt>' \
  -e Api__IpHeader=CF-Connecting-IP \
  -e Api__AllowedOrigins__0=https://alenalex.me \
  -e Github__Token='github_pat_...' \
  -e Discord__BotToken='...' -e Discord__GuildId=... -e Discord__ChannelId=... -e Discord__UserId=... \
  -e Homelab__Services__0__Name=jellyfin -e Homelab__Services__0__Url=http://jellyfin:8096 \
  alenalex-api
```

(Or put the same `Key=value` lines in a file and use `--env-file`.)

The final image is `runtime-deps:10.0-noble-chiseled` (no shell, no .NET runtime, non-root
`app` user) listening on 8080; it writes nothing to disk. NativeAOT can't cross-compile between CPU architectures, so the
build stage runs on the target platform; building `linux/amd64` on Apple Silicon works under
emulation but is slow.

Put a reverse proxy or Cloudflare Tunnel in front, terminating TLS for `api.alenalex.me` and
forwarding to port 8080. Behind Cloudflare set `Api__IpHeader=CF-Connecting-IP` so likes and
rate limits see the visitor's IP rather than the proxy's.
