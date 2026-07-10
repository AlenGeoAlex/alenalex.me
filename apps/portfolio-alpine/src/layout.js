// layout.js
// Shared topbar and statusbar for all pages.
// Two entry points:
//   mountLayout({ title, activePage })     — main site pages
//   mountPostLayout({ slug, hash, date })  — post pages

(function () {

    const NAV = [
        { label: '~',       page: 'home',    href: '/index.html'   },
        { label: 'writing', page: 'writing', href: '/writing.html' },
        { label: 'work',    page: 'work',    href: '/work.html'    },
        { label: 'now',     page: 'now',     href: '/now.html'     },
    ];

    // ── Shared: statusbar clock ───────────────────────────────
    function startClock(elId) {
        const el = document.getElementById(elId);
        function tick() {
            const n = new Date();
            const p = v => String(v).padStart(2, '0');
            if (el) el.textContent =
                `${p(n.getUTCHours())}:${p(n.getUTCMinutes())}:${p(n.getUTCSeconds())} UTC`;
        }
        tick();
        setInterval(tick, 1000);
    }

    // ── Shared: statusbar footer links ────────────────────────
    function buildFooterLinks(containerId) {
        const email = document.getElementById('sb-email');
        if (email && window.CONFIG) email.textContent = CONFIG.EMAIL;

        const links = [
            { label: 'github',   href: 'https://github.com/'      + (CONFIG?.GITHUB_USER || '') },
            { label: 'linkedin', href: 'https://linkedin.com/in/' + (CONFIG?.GITHUB_USER || '') },
            { label: 'rss',      href: '/feed.xml' },
        ];

        const container = document.getElementById(containerId);
        if (!container) return;

        links.forEach(({ label, href }) => {
            const a = document.createElement('a');
            a.href = href;
            a.textContent = label;
            if (label !== 'rss') { a.target = '_blank'; a.rel = 'noopener'; }
            container.appendChild(a);
        });
    }

    // ── Main site topbar ──────────────────────────────────────
    function buildTopbar(title, activePage) {
        const header = document.createElement('header');
        header.className = 'topbar';
        header.setAttribute('role', 'banner');

        const left = document.createElement('div');
        left.className = 'topbar-left';
        left.innerHTML = `
            <div class="topbar-dots" aria-hidden="true">
                <div class="topbar-dot"></div>
                <div class="topbar-dot"></div>
                <div class="topbar-dot"></div>
            </div>
            <span class="topbar-title">${title}</span>
        `;

        const nav = document.createElement('nav');
        nav.className = 'topbar-nav';
        nav.setAttribute('aria-label', 'Main navigation');

        NAV.forEach(({ label, page, href }) => {
            const a = document.createElement('a');
            a.href = href;
            a.textContent = label;
            if (page === activePage) {
                a.classList.add('active');
                a.setAttribute('aria-current', 'page');
            }
            nav.appendChild(a);
        });

        header.appendChild(left);
        header.appendChild(nav);
        return header;
    }

    // ── Main site statusbar ───────────────────────────────────
    function buildStatusbar() {
        const footer = document.createElement('footer');
        footer.className = 'statusbar';
        footer.setAttribute('role', 'contentinfo');

        footer.innerHTML = `
            <div class="statusbar-left">
                <span class="sdot sdot-online" aria-label="Status: nominal"></span>
                <span>systems nominal</span>
                <span class="sb-sep">·</span>
                <span id="sb-clock"></span>
                <span class="sb-sep">·</span>
                <span id="sb-email"></span>
            </div>
            <div class="statusbar-right" id="sb-links"></div>
        `;

        return footer;
    }

    // ── Post topbar ───────────────────────────────────────────
    function buildPostTopbar({ hash, date }) {
        const header = document.createElement('header');
        header.className = 'post-topbar';

        header.innerHTML = `
            <a class="post-back" href="/writing.html">← writing</a>
            <div class="post-topbar-right">
                <span class="post-meta-top">${hash} · ${date}</span>
                <button class="post-theme-toggle" id="theme-toggle">[ light ]</button>
            </div>
        `;

        return header;
    }

    // ── Post statusbar ────────────────────────────────────────
    function buildPostStatusbar() {
        const footer = document.createElement('footer');
        footer.className = 'post-statusbar';

        footer.innerHTML = `
            <div class="post-statusbar-left">
                <span>alenalex.me</span>
                <span class="post-sb-sep">·</span>
                <span id="post-sb-clock"></span>
            </div>
            <div class="post-statusbar-right">
                <a href="https://github.com/${window.CONFIG?.GITHUB_USER || 'AlenGeoAlex'}"
                   target="_blank" rel="noopener">github</a>
                <a href="/writing.html">writing</a>
                <a href="/index.html">~</a>
            </div>
        `;

        return footer;
    }

    // ── Mount: main site pages ────────────────────────────────
    window.mountLayout = function ({ title, activePage }) {
        const shell = document.querySelector('.page-shell');
        if (!shell) { console.warn('layout.js: .page-shell not found'); return; }

        shell.insertBefore(buildTopbar(title, activePage), shell.firstChild);
        shell.appendChild(buildStatusbar());

        startClock('sb-clock');
        requestAnimationFrame(() => buildFooterLinks('sb-links'));
    };

    // ── Mount: post pages ─────────────────────────────────────
    window.mountPostLayout = function ({ hash, date }) {
        const page = document.querySelector('.post-page');
        if (!page) { console.warn('layout.js: .post-page not found'); return; }

        page.insertBefore(buildPostTopbar({ hash, date }), page.firstChild);
        page.appendChild(buildPostStatusbar());

        startClock('post-sb-clock');
    };

})();