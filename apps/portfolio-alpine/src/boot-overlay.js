// boot-overlay.js — zero dependencies, vanilla JS only

(function () {

  const BOOT_LINES = [
    { label: 'BIOS v1.0',                      status: 'OK',   ok: true  },
    { label: 'Loading kernel',                  status: 'OK',   ok: true  },
    { label: 'Mounting /home/alen',             status: 'OK',   ok: true  },
    { label: 'Starting services',               status: 'OK',   ok: true  },
    { label: 'Checking caffeine levels',        status: 'WARN', ok: false },
    { label: 'Initialising personality module', status: 'OK',   ok: true  },
  ];

  const DOTS = ' .......................... ';

  function wait(ms) {
    return new Promise(r => setTimeout(r, ms));
  }

  function typeInto(el, text, msPer) {
    return new Promise(resolve => {
      let i = 0;
      el.textContent = '';
      const iv = setInterval(() => {
        el.textContent = text.slice(0, ++i);
        if (i >= text.length) { clearInterval(iv); resolve(); }
      }, msPer);
    });
  }

  function buildOverlay() {
    const overlay = document.createElement('div');
    overlay.id = 'boot-overlay';

    const linesDiv = document.createElement('div');
    linesDiv.className = 'boot-lines';

    BOOT_LINES.forEach(({ ok }) => {
      const row = document.createElement('div');
      row.className = 'boot-line';
      row.innerHTML =
        '<span class="bl-label"></span>' +
        '<span class="bl-dots"></span>' +
        '<span class="bl-status ' + (ok ? 'ok' : 'warn') + '"></span>';
      linesDiv.appendChild(row);
    });

    const logo = document.createElement('div');
    logo.className = 'boot-logo';
    overlay.appendChild(linesDiv);
    overlay.appendChild(logo);
    document.body.prepend(overlay);
    return overlay;
  }

  async function runSequence(overlay) {
    const rows = overlay.querySelectorAll('.boot-line');
    const logo = overlay.querySelector('.boot-logo');

    for (let i = 0; i < BOOT_LINES.length; i++) {
      const { label, status } = BOOT_LINES[i];
      const row      = rows[i];
      const labelEl  = row.querySelector('.bl-label');
      const dotsEl   = row.querySelector('.bl-dots');
      const statusEl = row.querySelector('.bl-status');

      row.style.opacity = '1';
      await typeInto(labelEl, label, 22);
      await typeInto(dotsEl, DOTS, 5);
      statusEl.textContent = status;
      await wait(50);
    }

    await wait(120);
    await typeInto(logo, 'alenalex.me', 45);
    await wait(500);

    // Simple fade out — pure CSS transition, no anime needed
    overlay.style.transition = 'opacity 0.35s ease';
    overlay.style.opacity = '0';
    await wait(380);
    overlay.remove();
  }

  window.runBootOverlay = function (onComplete) {
    // Uncomment to force replay during dev:
    // sessionStorage.removeItem(CONFIG.BOOT_SESSION_KEY);

    if (sessionStorage.getItem(CONFIG.BOOT_SESSION_KEY)) {
      onComplete();
      return;
    }

    const overlay = buildOverlay();

    runSequence(overlay).then(function () {
      sessionStorage.setItem(CONFIG.BOOT_SESSION_KEY, '1');
      onComplete();
    });
  };

})();