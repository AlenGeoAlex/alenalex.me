// guestbook.js
// Alpine.js component for the guestbook column.
// Phase 1: static seed data. Phase 2: wire to /api/guestbook.

const ADJS  = ['anxious','sleepy','caffeinated','recursive','async',
                'stochastic','idempotent','fuzzy','latent','verbose',
                'silent','cursed','haunted','elegant','chaotic','stubborn','pensive'];

const NOUNS = ['raccoon','kernel','daemon','socket','pointer','mutex',
               'goblin','wizard','buffer','cron','packet','thread',
               'shard','replica','artifact','phantom','bard'];

function genName() {
  return ADJS[Math.floor(Math.random() * ADJS.length)]
    + '-' + NOUNS[Math.floor(Math.random() * NOUNS.length)];
}

// ── Static seed entries (Phase 1 placeholder) ───────────────
const SEED_ENTRIES = [
  {
    id: '001',
    name: 'caffeinated-goblin',
    date: '2025-06-12',
    message: 'Redis 10s to under a second — I need to know more about how that actually worked.',
    likes: 3,
  },
  {
    id: '002',
    name: 'recursive-daemon',
    date: '2025-05-28',
    message: 'The AGPL post saved me from a very bad licensing decision. Thank you.',
    likes: 7,
  },
  {
    id: '003',
    name: 'latent-phantom',
    date: '2025-05-01',
    message: 'This is the terminal aesthetic done right. Most people get it wrong.',
    likes: 4,
  },
  {
    id: '004',
    name: 'pensive-shard',
    date: '2025-04-14',
    message: 'Allotment post was unexpectedly moving. Backend engineer / gardener combo is rare.',
    likes: 2,
  },
  {
    id: '005',
    name: 'async-wizard',
    date: '2025-03-30',
    message: 'Cork represent.',
    likes: 1,
  },
];

function guestbookComponent() {
  return {
    entries: [...SEED_ENTRIES],
    authorName: genName(),
    message: '',
    pending: false,
    pendingMsg: '',
    liked: new Set(JSON.parse(localStorage.getItem(CONFIG.LIKED_KEY) || '[]')),

    reroll() {
      this.authorName = genName();
    },

    isLiked(id) {
      return this.liked.has(id);
    },

    toggleLike(entry) {
      if (this.liked.has(entry.id)) {
        this.liked.delete(entry.id);
        entry.likes = Math.max(0, entry.likes - 1);
      } else {
        this.liked.add(entry.id);
        entry.likes += 1;
      }
      localStorage.setItem(CONFIG.LIKED_KEY, JSON.stringify([...this.liked]));
      // Phase 2: POST /api/guestbook/:id/like
    },

    async submit() {
      const msg = this.message.trim();
      if (msg.length < CONFIG.GB_MIN_CHARS || msg.length > CONFIG.GB_MAX_CHARS) return;

      // Optimistic render
      const optimistic = {
        id: 'pending-' + Date.now(),
        name: this.authorName,
        date: new Date().toISOString().slice(0, 10),
        message: msg,
        likes: 0,
        pending: true,
      };

      this.entries.unshift(optimistic);
      this.message = '';
      this.pending = true;
      this.pendingMsg = '⟳ pending moderation';

      // Phase 2: POST to /api/guestbook
      // const res = await fetch(`${CONFIG.API_BASE}/api/guestbook`, {
      //   method: 'POST',
      //   headers: { 'Content-Type': 'application/json' },
      //   body: JSON.stringify({ name: this.authorName, message: msg }),
      // });

      setTimeout(() => {
        this.pending = false;
        this.pendingMsg = '';
      }, 4000);
    },

    handleKeydown(e) {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        this.submit();
      }
    },
  };
}