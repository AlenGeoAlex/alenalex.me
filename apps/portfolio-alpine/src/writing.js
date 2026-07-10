// writing.js
// Fetches .blogspec.json from jsDelivr and renders the git-log list.
// Preview mode: ?preview=<sha> makes drafts visible and appends sha to post links.

const BLOGSPEC_URL =
  "https://cdn.jsdelivr.net/gh/AlenGeoAlex/alenalex.me@main/blogs/.blogspec.json";

const STATIC_POSTS = [
  {
    title: "Games played in 2024",
    slug: "games-2024",
    date: "2024-12-31",
    published: true,
    icon: "🎮",
    tags: ["gaming", "personal"],
    excerpt:
      "A year in games. What I finished, what I abandoned, and what surprised me.",
    hash: "a3f9e2",
  },
  {
    title: "Beware of poison in the source",
    slug: "poison-in-the-source",
    date: "2024-10-24",
    published: true,
    icon: "⚗️",
    tags: ["security", "open-source", "backend"],
    excerpt: "Supply chain attacks are patient. Most defences are not.",
    hash: "b7c1d0",
  },
  {
    title: "Common misconceptions of the AGPL",
    slug: "misconceptions-agpl",
    date: "2024-10-02",
    published: true,
    icon: "📜",
    tags: ["open-source", "licensing"],
    excerpt:
      "The AGPL scares people who haven't read it. Here's what it actually says.",
    hash: "d2a88f",
  },
  {
    title: "Lessons from the allotment",
    slug: "lessons-allotment",
    date: "2024-07-02",
    published: true,
    icon: "🌱",
    tags: ["personal"],
    excerpt: "Things software taught me about gardening, and vice versa.",
    hash: "f03c77",
  },
  {
    title: "On distributed tracing in a monolith",
    slug: "distributed-tracing-monolith",
    date: "2024-05-14",
    published: false,
    icon: "🔍",
    tags: ["backend", "observability"],
    excerpt:
      "You don't need microservices to benefit from distributed tracing.",
    hash: "e91bc3",
  },
];

function writingComponent() {
  return {
    posts: [],
    grouped: {},
    years: [],
    allTags: [],
    loading: true,
    error: null,
    openSlug: null,
    previewSha: null,
    isPreview: false,

    // ── Filters ──────────────────────────────────────────────
    query: "",
    activeTags: [],

    async init() {
      const params = new URLSearchParams(window.location.search);
      this.previewSha = params.get("preview") || null;
      this.isPreview = !!this.previewSha;

      await this.loadPosts();

      // Focus search on '/' keypress
      document.addEventListener("keydown", (e) => {
        if (e.key === "/" && document.activeElement.tagName !== "INPUT") {
          e.preventDefault();
          document.getElementById("wr-search")?.focus();
        }
      });
    },

    async loadPosts() {
      this.loading = true;
      this.error = null;
      try {
        let posts;
        try {
          const res = await fetch(BLOGSPEC_URL);
          if (!res.ok) throw new Error(`HTTP ${res.status}`);
          const data = await res.json();
          posts = data.posts;
        } catch {
          console.warn("writing.js: falling back to static post data");
          posts = STATIC_POSTS;
        }

        posts.sort((a, b) => new Date(b.date) - new Date(a.date));
        this.posts = posts;
        this.buildTags(posts);
        this.buildGroups();
      } catch (err) {
        this.error = "failed to load posts · " + err.message;
      } finally {
        this.loading = false;
      }
    },

    buildTags(posts) {
      const seen = new Set();
      for (const post of posts) {
        if (!post.published && !this.isPreview) continue;
        for (const tag of post.tags || []) seen.add(tag);
      }
      this.allTags = [...seen].sort();
    },

    // Re-runs whenever query or activeTags changes
    buildGroups() {
      const q = this.query.trim().toLowerCase();
      const tags = this.activeTags;

      const filtered = this.posts.filter((post) => {
        // Always hide drafts unless preview mode
        if (!post.published && !this.isPreview) return false;

        // Text filter — match title or excerpt
        if (
          q &&
          !post.title.toLowerCase().includes(q) &&
          !(post.excerpt || "").toLowerCase().includes(q)
        )
          return false;

        // Tag filter — post must have ALL active tags
        if (tags.length && !tags.every((t) => (post.tags || []).includes(t)))
          return false;

        return true;
      });

      const groups = {};
      for (const post of filtered) {
        const year = post.date.slice(0, 4);
        if (!groups[year]) groups[year] = [];
        groups[year].push(post);
      }
      this.grouped = groups;
      this.years = Object.keys(groups).sort((a, b) => b - a);
    },

    toggleTag(tag) {
      if (this.activeTags.includes(tag)) {
        this.activeTags = this.activeTags.filter((t) => t !== tag);
      } else {
        this.activeTags = [...this.activeTags, tag];
      }
      this.openSlug = null;
      this.buildGroups();
    },

    onQueryInput() {
      this.openSlug = null;
      this.buildGroups();
    },

    clearFilters() {
      this.query = "";
      this.activeTags = [];
      this.openSlug = null;
      this.buildGroups();
    },

    get hasFilters() {
      return this.query.trim().length > 0 || this.activeTags.length > 0;
    },

    get totalVisible() {
      return Object.values(this.grouped).reduce((n, arr) => n + arr.length, 0);
    },

    isVisible(post) {
      return post.published || this.isPreview;
    },

    toggle(post) {
      if (!post.published && !this.isPreview) return;
      this.openSlug = this.openSlug === post.slug ? null : post.slug;
    },

    postHref(post) {
      const base = `writing/${post.slug}/index.html`;
      return this.isPreview ? `${base}?preview=${this.previewSha}` : base;
    },

    redact(str) {
      return (str || "").replace(/[^\s]/g, "█");
    },
  };
}
