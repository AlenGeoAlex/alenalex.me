// work.js
// GitHub repo constellation using D3 force simulation.
// Tries backend proxy first, falls back to GitHub API directly.

// ── Language config ───────────────────────────────────────────
// Terminal-palette colours + devicon CDN icons per language.
// Add more entries here as needed.
const LANG_CONFIG = {
    'JavaScript': { color: '#8a7a3a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/javascript/javascript-original.svg' },
    'TypeScript': { color: '#3a5f8a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/typescript/typescript-original.svg' },
    'Python':     { color: '#4a7a5a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/python/python-original.svg' },
    'Go':         { color: '#3a7a8a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/go/go-original.svg' },
    'Rust':       { color: '#8a5a3a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/rust/rust-original.svg' },
    'C#':         { color: '#5a3a8a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/csharp/csharp-original.svg' },
    'Java':       { color: '#8a3a3a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/java/java-original.svg' },
    'HTML':       { color: '#8a4a3a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/html5/html5-original.svg' },
    'CSS':        { color: '#3a4a8a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/css3/css3-original.svg' },
    'PHP':        { color: '#5a4a7a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/php/php-original.svg' },
    'Vue':        { color: '#3a7a5a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/vuejs/vuejs-original.svg' },
    'Swift':      { color: '#8a4a3a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/swift/swift-original.svg' },
    'Kotlin':     { color: '#5a3a7a', icon: 'https://cdn.jsdelivr.net/gh/devicons/devicon/icons/kotlin/kotlin-original.svg' },
    'Shell':      { color: '#4a6a4a', icon: null },
    'default':    { color: '#4a4a44', icon: null },
};

function langColor(lang) {
    return (LANG_CONFIG[lang] || LANG_CONFIG['default']).color;
}

function langIcon(lang) {
    return (LANG_CONFIG[lang] || LANG_CONFIG['default']).icon;
}

// ── Alpine component ──────────────────────────────────────────
function workComponent() {
    return {
        repos:       [],
        pinnedSlugs: new Set(),
        loading:     true,
        usingFallback: false,
        card:        null,   // currently open card { repo, x, y }
        simulation:  null,
        svg:         null,

        async init() {
            await this.loadRepos();
        },

        async loadRepos() {
            this.loading = true;
            const user   = window.CONFIG?.GITHUB_USER || 'AlenGeoAlex';
            const base   = window.CONFIG?.API_BASE;

            let repos = [], pinned = [];

            // ── Try backend proxy ─────────────────────────────
            if (base) {
                try {
                    const [rRes, pRes] = await Promise.all([
                        fetch(`${base}/api/github/repos`),
                        fetch(`${base}/api/github/pinned`),
                    ]);
                    if (!rRes.ok || !pRes.ok) throw new Error('backend error');
                    repos  = await rRes.json();
                    pinned = await pRes.json();
                } catch {
                    this.usingFallback = true;
                }
            } else {
                this.usingFallback = true;
            }

            // ── Fallback: GitHub API directly ─────────────────
            if (this.usingFallback) {
                try {
                    const rRes = await fetch(
                        `https://api.github.com/users/${user}/repos?per_page=100&sort=updated`
                    );
                    repos = await rRes.json();
                    // GitHub doesn't expose pinned via REST — skip pinned in fallback
                    pinned = [];
                } catch (err) {
                    console.error('work.js: GitHub API failed', err);
                    this.loading = false;
                    return;
                }
            }

            this.pinnedSlugs = new Set((pinned || []).map(p => p.name || p));
            this.repos = (repos || []).filter(r => !r.fork);
            this.loading = false;

            await this.$nextTick();
            this.drawConstellation();
        },

        drawConstellation() {
            const container = document.getElementById('work-canvas-container');
            if (!container || !this.repos.length) return;

            const W = container.clientWidth;
            const H = container.clientHeight;

            // ── D3 setup ──────────────────────────────────────
            const svg = d3.select('#work-svg')
                .attr('width', W)
                .attr('height', H);

            // Clear any previous render
            svg.selectAll('*').remove();
            this.svg = svg;

            const self = this;

            // Node sizing
            const starScale = d3.scaleSqrt()
                .domain([0, d3.max(this.repos, r => r.stargazers_count) || 1])
                .range([6, 22]);

            const nodes = this.repos.map(r => ({
                id:       r.name,
                repo:     r,
                pinned:   this.pinnedSlugs.has(r.name),
                r:        (this.pinnedSlugs.has(r.name) ? 4 : 0) + starScale(r.stargazers_count || 0),
                color:    langColor(r.language),
                icon:     langIcon(r.language),
            }));

            // ── Force simulation ──────────────────────────────
            const simulation = d3.forceSimulation(nodes)
                .force('charge',  d3.forceManyBody().strength(-60))
                .force('center',  d3.forceCenter(W / 2, H / 2))
                .force('collide', d3.forceCollide(d => d.r + 8))
                .force('x',       d3.forceX(W / 2).strength(0.04))
                .force('y',       d3.forceY(H / 2).strength(0.04));

            this.simulation = simulation;

            // ── Edge glow filter ──────────────────────────────
            const defs = svg.append('defs');
            defs.append('filter')
                .attr('id', 'node-glow')
                .append('feGaussianBlur')
                .attr('stdDeviation', '2.5')
                .attr('result', 'blur');

            // Pinned glow filter
            const pinnedFilter = defs.append('filter').attr('id', 'pinned-glow');
            pinnedFilter.append('feGaussianBlur').attr('stdDeviation', '3').attr('result', 'blur');
            pinnedFilter.append('feMerge').selectAll('feMergeNode')
                .data(['blur', 'SourceGraphic'])
                .enter().append('feMergeNode')
                .attr('in', d => d);

            // ── Node groups ───────────────────────────────────
            const node = svg.append('g')
                .selectAll('g')
                .data(nodes)
                .enter()
                .append('g')
                .attr('class', 'node-group')
                .style('cursor', 'pointer')
                .call(d3.drag()
                    .on('start', (event, d) => {
                        if (!event.active) simulation.alphaTarget(0.3).restart();
                        d.fx = d.x; d.fy = d.y;
                    })
                    .on('drag', (event, d) => {
                        d.fx = event.x; d.fy = event.y;
                    })
                    .on('end', (event, d) => {
                        if (!event.active) simulation.alphaTarget(0);
                        d.fx = null; d.fy = null;
                    })
                );

            // Pinned outer ring
            node.filter(d => d.pinned)
                .append('circle')
                .attr('r', d => d.r + 5)
                .attr('fill', 'none')
                .attr('stroke', '#bfbfb7')
                .attr('stroke-width', 0.75)
                .attr('stroke-dasharray', '3 3')
                .attr('opacity', 0.5);

            // Main circle
            node.append('circle')
                .attr('r', d => d.r)
                .attr('fill', d => d.color)
                .attr('opacity', d => d.pinned ? 0.9 : 0.7)
                .attr('filter', d => d.pinned ? 'url(#pinned-glow)' : null)
                .attr('stroke', d => d.pinned ? '#bfbfb7' : d.color)
                .attr('stroke-width', d => d.pinned ? 1 : 0.5)
                .attr('stroke-opacity', 0.6);

            // Hover icon (HTML overlay, not SVG)
            const hoverIcon = document.getElementById('node-hover-icon');

            // Click + hover handlers
            node
                .on('mouseenter', function(event, d) {
                    if (!d.icon) return;
                    const rect = container.getBoundingClientRect();
                    hoverIcon.style.left = (d.x) + 'px';
                    hoverIcon.style.top  = (d.y) + 'px';
                    hoverIcon.querySelector('img').src = d.icon;
                    hoverIcon.classList.add('visible');
                })
                .on('mouseleave', function() {
                    hoverIcon.classList.remove('visible');
                })
                .on('click', function(event, d) {
                    event.stopPropagation();
                    hoverIcon.classList.remove('visible');
                    self.openCard(d, event);
                });

            // ── Tick ──────────────────────────────────────────
            simulation.on('tick', () => {
                node.attr('transform', d => `translate(${d.x},${d.y})`);

                // Keep hover icon in sync if visible
                if (hoverIcon.classList.contains('visible')) {
                    // will update on next mouseenter
                }
            });

            // Close card on canvas click
            svg.on('click', () => { this.card = null; });
        },

        openCard(d, event) {
            const container = document.getElementById('work-canvas-container');
            const rect      = container.getBoundingClientRect();
            const cardW     = 300;
            const cardH     = 200;

            let x = d.x + d.r + 12;
            let y = d.y - 60;

            // Keep within bounds
            if (x + cardW > container.clientWidth  - 16) x = d.x - cardW - d.r - 12;
            if (y + cardH > container.clientHeight - 16) y = container.clientHeight - cardH - 16;
            if (y < 8) y = 8;

            this.card = { repo: d.repo, pinned: d.pinned, x, y };
        },

        closeCard() {
            this.card = null;
        },

        langColor(lang) { return langColor(lang); },
        langIcon(lang)  { return langIcon(lang);  },

        repoUrl(repo) {
            return repo.html_url || `https://github.com/${window.CONFIG?.GITHUB_USER}/${repo.name}`;
        },
    };
}