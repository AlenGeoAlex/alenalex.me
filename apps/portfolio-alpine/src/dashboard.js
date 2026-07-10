// dashboard.js
// Alpine.js component for the right dashboard column.
// Phase 1: static data + live UTC clock.
// Phase 2: wire GitHub REST, Spotify, Discord, Homelab APIs.

function dashboardComponent() {
  return {
    // ── Clock ─────────────────────────────────────────────────
    clockTime: '',
    clockDate: '',

    // ── GitHub (Phase 2: live) ────────────────────────────────
    github: {
      repos:      24,
      stars:      2,
      topLang:    'HTML',
      lastPush:   '265d ago',
    },

    // ── Status ────────────────────────────────────────────────
    discord:  'online',   // online | idle | dnd | offline
    homelab:  { services: 3, status: 'green' },

    // ── Spotify ───────────────────────────────────────────────
    spotify: { track: null, artist: null, playing: false },

    init() {
      this.tickClock();
      setInterval(() => this.tickClock(), 1000);

      // Phase 2: uncomment these
      // this.fetchGithub();
      // this.pollSpotify();
      // this.pollDiscord();
      // this.pollHomelab();
    },

    tickClock() {
      const now = new Date();
      const h = String(now.getUTCHours()).padStart(2, '0');
      const m = String(now.getUTCMinutes()).padStart(2, '0');
      const s = String(now.getUTCSeconds()).padStart(2, '0');
      this.clockTime = `${h}:${m}:${s}`;

      const months = ['jan','feb','mar','apr','may','jun',
                      'jul','aug','sep','oct','nov','dec'];
      const d  = now.getUTCDate();
      const mo = months[now.getUTCMonth()];
      const y  = now.getUTCFullYear();
      this.clockDate = `${d} ${mo} ${y} utc`;
    },

    discordDot() {
      return {
        online:  'sdot sdot-online',
        idle:    'sdot sdot-idle',
        dnd:     'sdot sdot-dnd',
        offline: 'sdot sdot-offline',
      }[this.discord] || 'sdot sdot-offline';
    },

    homelabClass() {
      return this.homelab.status === 'green' ? 'ok' : 'warn';
    },

    spotifyLine() {
      if (!this.spotify.playing || !this.spotify.track) return null;
      return `${this.spotify.track} — ${this.spotify.artist}`;
    },

    // ── Phase 2 methods (stubs) ───────────────────────────────
    async fetchGithub() {
      const cache = sessionStorage.getItem('gh-cache');
      if (cache) {
        const { data, ts } = JSON.parse(cache);
        if (Date.now() - ts < CONFIG.CACHE_GITHUB) {
          this.github = data;
          return;
        }
      }
      // fetch logic goes here
    },

    async pollSpotify() {
      const fetch_ = async () => {
        try {
          const res  = await fetch(`${CONFIG.API_BASE}/api/now-playing`);
          const data = await res.json();
          this.spotify = data;
        } catch { /* unreachable — show dash */ }
      };
      await fetch_();
      setInterval(fetch_, CONFIG.POLL_SPOTIFY);
    },

    async pollDiscord() {
      const fetch_ = async () => {
        try {
          const res  = await fetch(`${CONFIG.API_BASE}/api/discord-status`);
          const data = await res.json();
          this.discord = data.status;
        } catch { this.discord = 'offline'; }
      };
      await fetch_();
      setInterval(fetch_, CONFIG.POLL_DISCORD);
    },

    async pollHomelab() {
      const fetch_ = async () => {
        try {
          const res  = await fetch(`${CONFIG.API_BASE}/api/homelab`);
          const data = await res.json();
          this.homelab = data;
        } catch { this.homelab = { services: 0, status: 'down' }; }
      };
      await fetch_();
      setInterval(fetch_, CONFIG.POLL_HOMELAB);
    },
  };
}