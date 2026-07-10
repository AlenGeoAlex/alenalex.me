

(function () {
  let recentPosts = [];

  try{
    recentPosts = INJECTED_WRITINGS
  }catch (e){
    // ignore - If for some reason, it failed to add writing.constant.ts, then don't fail, rather show it empty
  }

  const GITHUB_DATA = {
    public_repos: 24,
    total_stars: 2,
    top_language: "HTML",
    last_commit: "265d ago",
  };

  // ── Typewriter ──────────────────────────────────────────────
  function typeWriter(el, text, msPerChar = 28) {
    return new Promise((resolve) => {
      if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
        el.textContent += text;
        resolve();
        return;
      }
      let i = 0;
      const iv = setInterval(() => {
        el.textContent += text[i++];
        if (i >= text.length) {
          clearInterval(iv);
          resolve();
        }
      }, msPerChar);
    });
  }

  function wait(ms) {
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) ms = 0;
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  function append(el) {
    const terminal = document.getElementById("terminal");
    terminal.appendChild(el);
    // Auto-scroll
    terminal.scrollTop = terminal.scrollHeight;
  }

  function makeLine(cls = "", html = "") {
    const el = document.createElement("span");
    el.className = `term-line ${cls}`;
    if (html) el.innerHTML = html;
    return el;
  }

  function makeBlock(cls = "", html = "") {
    const el = document.createElement("div");
    el.className = cls;
    if (html) el.innerHTML = html;
    return el;
  }

  function line(cls, html) {
    const el = makeLine(cls, html);
    append(el);
    return el;
  }

  async function typePrompt(text) {
    const el = makeLine("term-prompt");
    el.textContent = "$ ";
    append(el);
    await wait(80);
    await typeWriter(el, text, 26);
    await wait(100);
    return el;
  }

  window.runTerminal = async function () {
    // init line
    line("term-line term-init", "alenalex.me — init");
    await wait(60);

    // [OK] status lines
    const statusLines = [
      {
        ok: true,
        text: "backend engineering · distributed systems · reliability",
      },
      {
        ok: true,
        text: "stack: .NET · Java · Angular · Rust · postgres · redis · docker",
      },
      // {
      //   ok: true,
      //   text: "research: deep learning · ML · scalable systems @ UCC",
      // },
      { ok: true, text: "homelab: 3 services running · all green" },
      { ok: false, text: "caffeine_reserves: critically low · non-fatal" },
    ];

    for (const { ok, text } of statusLines) {
      if (ok) {
        line(
          "term-line",
          `<span class="term-ok-prefix">[ OK  ]</span> <span class="term-ok-text">${text}</span>`,
        );
      } else {
        line(
          "term-line",
          `<span class="term-warn-prefix">[WARN ]</span> <span class="term-ok-text">${text}</span>`,
        );
      }
      await wait(40);
    }

    line("term-spacer");
    await wait(80);
    line("term-line term-ready", "ready.");

    await wait(500);
    line("term-spacer");

    await typePrompt("whoami");

    const nameEl = document.createElement("div");
    nameEl.className = "term-name";
    nameEl.textContent = "Alen Alex.";
    append(nameEl);
    await wait(60);
    nameEl.classList.add("visible");

    await wait(80);
    line("term-line term-sub", "");
    line(
      "term-line term-sub",
      "  MSc Computing Science · Software Engineer · Cork, Ireland",
    );
    line("term-line term-sub", "");
    line(
      "term-line term-sub",
      "  I build backend systems and write about whatever I'm learning.",
    );

    await wait(400);
    line("term-spacer");

    await typePrompt("fetch --source github");

    await wait(120);

    const kvPairs = [
      ["public repos", String(GITHUB_DATA.public_repos)],
      ["total stars", String(GITHUB_DATA.total_stars)],
      ["top language", GITHUB_DATA.top_language],
      ["last commit", GITHUB_DATA.last_commit],
    ];

    line("term-line term-sub", "");
    for (const [k, v] of kvPairs) {
      const row = document.createElement("div");
      row.className = "term-kv";
      row.innerHTML = `<span class="term-kv-key">  ${k}</span><span class="term-kv-val">${v}</span>`;
      append(row);
      await wait(30);
    }

    line("term-line term-sub", "");
    line(
      "term-line term-nav-hint",
      '  → <a href="work.html" class="term-link">work /repos</a>',
    );

    await wait(400);
    line("term-spacer");

    await typePrompt("cat writing/recent.log");

    await wait(80);
    line("term-line term-sub", "");

    for (const p of recentPosts) {
      const row = document.createElement("div");
      row.className = "term-log-row";
      row.innerHTML =
        `<span class="term-log-hash">${p.hash}</span>` +
        `<span class="term-log-date">  ${p.date}</span>` +
        `<a href="writing/${p.slug}"><span class="term-log-title">  ${p.icon}  ${p.title}</span></a>`;
      append(row);
      await wait(30);
    }

    line("term-line term-sub", "");
    line(
      "term-line term-nav-hint",
      '  → <a href="writing.html" class="term-link">writing /all</a>',
    );

    await wait(1100);
    line("term-spacer");

    // ── $ man alen ────────────────────────────────────────────
    await typePrompt("man alen");
    await wait(80);

    line("term-line term-sub", "");

    // Man page header
    line(
      "term-line",
      `<span class="term-ok-text">ALEN(1)&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; User Commands&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; ALEN(1)</span>`,
    );
    await wait(30);
    line("term-line term-sub", "");
    await wait(20);

    line("term-line term-man-section", "NAME");
    await wait(20);
    line(
      "term-line term-man-body",
      "    alen -- backend engineer, writer, homelab operator",
    );
    await wait(40);

    line("term-line term-sub", "");
    line("term-line term-man-section", "SYNOPSIS");
    await wait(20);
    line(
      "term-line term-man-synopsis",
      "    alen [--build] [--optimise] [--write] [--learn-something-new]",
    );
    await wait(40);

    line("term-line term-sub", "");
    line("term-line term-man-section", "DESCRIPTION");
    await wait(20);
    line(
      "term-line term-man-body",
      "    Professional over-thinker of database schemas and API boundaries.",
    );
    await wait(20);
    line(
      "term-line term-man-body",
      "    Currently doing an MSc to make the over-engineering feel more legitimate.",
    );
    await wait(40);

    line("term-line term-sub", "");
    line("term-line term-man-section", "BUGS");
    await wait(20);
    line(
      "term-line term-man-body",
      '    Considers adding distributed tracing to personal projects "reasonable".',
    );
    await wait(20);
    line(
      "term-line term-man-body",
      "    Refuses to use a managed service if a self-hosted version exists.",
    );

    line("term-line term-sub", "");
    await wait(200);

    // Final prompt with blinking cursor
    const finalPrompt = makeLine("term-prompt");
    finalPrompt.innerHTML = '$ <span class="cursor"></span>';
    append(finalPrompt);
  };
})();
