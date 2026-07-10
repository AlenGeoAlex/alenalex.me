const CONFIG = {
  // Identity
  GITHUB_USER: "AlenGeoAlex",
  SITE_URL: "https://alenalex.me",
  SITE_NAME: "Alen Alex",
  EMAIL: "contact@alenalex.me",

  // Monorepo structure
  BLOG_REPO: "AlenGeoAlex/alenalex.me",
  BLOG_PATH: "blogs",
  POSTS_PATH: "blogs/posts",
  NOW_PATH: "blogs/now",
  GENERATED_PATH: "blogs/generated",

  BLOGSPEC_URL:
    "https://raw.githubusercontent.com/AlenGeoAlex/alenalex.me/main/blogs/.blogspec.json",
  CDN_BASE: "https://cdn.jsdelivr.net/gh/AlenGeoAlex/alenalex.me@main",
  GITHUB_API: "https://api.github.com",

  API_BASE: "https://api.alenalex.me",

  CACHE_GITHUB: 5 * 60 * 1000, // 5 min
  CACHE_BLOGSPEC: 5 * 60 * 1000, // 5 min
  CACHE_CONTRIBS: 3 * 60 * 60 * 1000, // 3 hours

  // Polling intervals (milliseconds)
  POLL_GUESTBOOK: 30 * 1000,
  POLL_SPOTIFY: 30 * 1000,
  POLL_HOMELAB: 60 * 1000,
  POLL_DISCORD: 60 * 1000,

  // Guestbook
  GB_MAX_CHARS: 200,
  GB_MIN_CHARS: 3,

  // Boot overlay
  BOOT_SESSION_KEY: "boot-seen",

  // Liked entries (localStorage)
  LIKED_KEY: "gb-liked",

  // Heatmap weeks by breakpoint
  HEATMAP_XL: 13,
  HEATMAP_LG: 10,
  HEATMAP_MD: 7,
};
