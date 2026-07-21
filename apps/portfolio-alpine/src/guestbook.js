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


async function fetchEntries() {
  const res = await fetch(`${CONFIG.API_BASE}/guestbook`);
  const data = await res.json();
  return (data?.entries ?? []).map(entry => ({
    ...entry,
    date: new Date(entry.created_at).toISOString().slice(0, 10),
  }));
}

function guestbookComponent() {


  return {
    entries: [],
    authorName: genName(),
    message: '',
    pending: false,
    loading: true,

    async init() {
      await this.loadRemote();
      this.loading = false;
    },

    async loadRemote() {
      this.entries = await fetchEntries();
    },

    reroll() {
      this.authorName = genName();
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

      const res = await fetch(`${CONFIG.API_BASE}/guestbook`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: this.authorName, message: msg }),
      });

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