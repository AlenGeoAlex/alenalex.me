// post.js
// Handles post page behaviour:
// - Light/dark mode toggle (persisted to localStorage)
// - Draft/preview mode (?preview=<sha>)
// - Revision history from GitHub Commits API
// - Prism theme swapping

const POST_CONFIG = {
  GITHUB_USER: window.CONFIG?.GITHUB_USER || "AlenGeoAlex",
  REPO: window.CONFIG?.REPO || "alenalex.me",
  BLOGS_PATH: "blogs/posts", // path inside repo where posts live
};

// ── Theme ─────────────────────────────────────────────────────
const THEME_KEY = "post-theme";

function getTheme() {
  return localStorage.getItem(THEME_KEY) || "dark";
}

function applyTheme(theme) {
  const root = document.documentElement;
  root.classList.toggle("light-mode", theme === "light");
  root.classList.toggle("dark-mode", theme === "dark");

  // Swap Prism theme stylesheets
  document.getElementById("prism-dark").disabled = theme === "light";
  document.getElementById("prism-light").disabled = theme === "dark";

  // Update toggle button label
  const btn = document.getElementById("theme-toggle");
  if (btn) btn.textContent = theme === "dark" ? "[ light ]" : "[ dark ]";

  localStorage.setItem(THEME_KEY, theme);
}

function toggleTheme() {
  applyTheme(getTheme() === "dark" ? "light" : "dark");
}

// ── Preview / draft ───────────────────────────────────────────
function getPreviewSha() {
  return new URLSearchParams(window.location.search).get("preview") || null;
}

// ── Revisions ─────────────────────────────────────────────────
async function loadRevisions(slug) {
  const { GITHUB_USER, REPO, BLOGS_PATH } = POST_CONFIG;
  const path = `${BLOGS_PATH}/${slug}/index.md`;
  const url = `https://api.github.com/repos/${GITHUB_USER}/${REPO}/commits?path=${path}&per_page=20`;

  try {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();
    return data.map((c) => ({
      sha: c.sha.slice(0, 7),
      fullSha: c.sha,
      date: c.commit.author.date.slice(0, 10),
      msg: c.commit.message.split("\n")[0],
    }));
  } catch (err) {
    console.warn("post.js: could not load revisions", err);
    return [];
  }
}

function renderRevisions(revisions, slug) {
  const list = document.getElementById("revisions-list");
  const btn = document.getElementById("revisions-toggle");
  const count = document.getElementById("revisions-count");

  if (!list) return;

  if (!revisions.length) {
    btn.textContent = "› revisions (none)";
    return;
  }

  count.textContent = revisions.length;

  revisions.forEach((r) => {
    const row = document.createElement("div");
    row.className = "post-revision-row";

    const shaUrl = `?preview=${r.fullSha}`;
    row.innerHTML =
      `<span class="post-revision-sha"><a href="${shaUrl}" title="preview this version">${r.sha}</a></span>` +
      `<span class="post-revision-date">${r.date}</span>` +
      `<span class="post-revision-msg">${r.msg}</span>`;

    list.appendChild(row);
  });
}

function initRevisions(slug) {
  const btn = document.getElementById("revisions-toggle");
  const list = document.getElementById("revisions-list");
  if (!btn || !list) return;

  let loaded = false;
  let open = false;

  btn.addEventListener("click", async () => {
    open = !open;
    btn.classList.toggle("open", open);
    list.classList.toggle("open", open);

    if (open && !loaded) {
      const count = document.getElementById("revisions-count");
      if (count) count.textContent = "…";
      const revisions = await loadRevisions(slug);
      renderRevisions(revisions, slug);
      loaded = true;
    }
  });
}

// ── Init ──────────────────────────────────────────────────────
document.addEventListener("DOMContentLoaded", () => {
  // Theme
  applyTheme(getTheme());
  const toggleBtn = document.getElementById("theme-toggle");
  if (toggleBtn) toggleBtn.addEventListener("click", toggleTheme);

  // Revisions
  const slug = document.querySelector("[data-slug]")?.dataset.slug;
  if (slug) initRevisions(slug);

  // Prism highlight
  if (window.Prism) Prism.highlightAll();
});
