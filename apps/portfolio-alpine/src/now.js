// now.js
// ── Config — update content here ─────────────────────────────
function nowComponent() {
  return {
    visible: false,
    visible2: false,
    visible3: false,
    visible4: false,
    visible5: false,

    currently: [
      {
        icon: "./assets/easetalk_logo.png",
        label: "EaseTalk",
        sub: "backend architect · clinical SLT platform",
        href: "https://www.easetalk.com",
      },
      {
        icon: "./assets/ucclogo.png",
        label: "MSc Computing Science",
        sub: "University College Cork",
        href: "https://www.ucc.ie",
      },
    ],

    horizon: [
      {
        icon: "./assets/rustlogo.png",
        label: "Rust",
        href: "https://www.rust-lang.org",
      },
      {
        icon: "./assets/cuda.png",
        label: "GPU programming",
        href: null,
      },
    ],

    outside: ["Getting fit", "Learning to skydive"],

    socials: [
      {
        key: "email",
        val: "contact@alenalex.me",
        href: "mailto:contact@alenalex.me",
      },
      {
        key: "github",
        val: "AlenGeoAlex",
        href: "https://github.com/AlenGeoAlex",
      },
      {
        key: "linkedin",
        val: "alengeoalex",
        href: "https://linkedin.com/in/alengeoalex",
      },
      {
        key: "discord",
        val: "@_creambun",
        href: "https://discord.com/users/_creambun",
      },
    ],

    init() {
      const reduced = window.matchMedia(
        "(prefers-reduced-motion: reduce)",
      ).matches;
      const delay = reduced ? 0 : 150;
      setTimeout(() => (this.visible = true), delay * 1);
      setTimeout(() => (this.visible2 = true), delay * 2);
      setTimeout(() => (this.visible3 = true), delay * 3);
      setTimeout(() => (this.visible4 = true), delay * 4);
      setTimeout(() => (this.visible5 = true), delay * 5);
    },
  };
}
