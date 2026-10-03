// Renderers for the pulsar plot. Kept out of the component so the component stays about state,
// and three.js is only ever loaded in the browser (dynamic import).
import type { ContributionDay } from '@core/models/github.model';

/** One line of the plot: a week of contributions. */
export interface PulsarWeek {
  days: ContributionDay[];
  total: number;
}

export interface PulsarRenderer {
  setData(weeks: PulsarWeek[]): void;
  /** Returns the index of the week under the pointer, or null. */
  pointer(cx: number | null, cy: number | null): number | null;
  dispose(): void;
}

/** WebGL first; falls back to a flat 2D canvas when WebGL isn't available. */
export async function createPulsarRenderer(canvas: HTMLCanvasElement, host: HTMLElement): Promise<PulsarRenderer> {
  try {
    return await createWebGlRenderer(canvas, host);
  } catch {
    return createFlatRenderer(canvas, host);
  }
}

/** Week profile shared by both renderers: a narrow bump per day, height by share of the busiest day. */
function profile(days: ContributionDay[] | null, max: number, samples: number): number[] {
  const ys = new Array(samples).fill(0);
  if (!days || max <= 0) return ys;
  days.forEach((d, i) => {
    if (!d.count) return;
    const centre = 0.22 + (i / 6) * 0.56; // days live in the middle band, the edges stay quiet
    const h = Math.pow(d.count / max, 0.65);
    for (let s = 0; s < samples; s++) {
      const x = s / (samples - 1);
      ys[s] += h * Math.exp(-(((x - centre) / 0.045) ** 2));
    }
  });
  return ys.map((y) => Math.min(y, 1.25));
}

/** Canvas 2D fallback for devices without WebGL: same lines, no tilt. */
function createFlatRenderer(canvas: HTMLCanvasElement, host: HTMLElement): PulsarRenderer {
  const ctx = canvas.getContext('2d')!;
  let weeks: PulsarWeek[] = [];
  let hover: number | null = null;
  let rows: number[] = [];
  const draw = () => {
    const dpr = Math.min(devicePixelRatio, 2);
    const w = host.clientWidth, h = host.clientHeight;
    canvas.width = w * dpr; canvas.height = h * dpr;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, w, h);
    const n = weeks.length || 53;
    const max = Math.max(1, ...weeks.flatMap((x) => x.days.map((d) => d.count)));
    const side = Math.min(w, h * 1.05);
    const left = (w - side) / 2, amp = side * 0.16, top = amp + 12, step = (h - top - 12) / n;
    rows = [];
    for (let i = 0; i < n; i++) {
      const base = top + i * step;
      rows.push(base);
      const ys = profile(weeks[i]?.days ?? null, max, 120);
      ctx.beginPath();
      ys.forEach((y, s) => {
        const x = left + (s / 119) * side;
        s ? ctx.lineTo(x, base - y * amp) : ctx.moveTo(x, base - y * amp);
      });
      ctx.lineTo(left + side, h); ctx.lineTo(left, h); ctx.closePath();
      ctx.fillStyle = '#0a0a0a'; ctx.fill();
      ctx.beginPath();
      ys.forEach((y, s) => {
        const x = left + (s / 119) * side;
        s ? ctx.lineTo(x, base - y * amp) : ctx.moveTo(x, base - y * amp);
      });
      ctx.strokeStyle = i === hover ? '#e5412f' : hover == null ? 'rgba(242,240,234,0.8)' : 'rgba(242,240,234,0.4)';
      ctx.lineWidth = i === hover ? 2.4 : 1.1;
      ctx.stroke();
    }
  };
  const ro = new ResizeObserver(draw);
  ro.observe(host);
  return {
    setData(w) { weeks = w; draw(); },
    pointer(cx, cy) {
      if (cx == null || cy == null) { hover = null; draw(); return null; }
      const y = cy - host.getBoundingClientRect().top;
      const i = rows.findIndex((r) => r >= y - 4);
      hover = i >= 0 && rows[i] - y < 40 ? i : null;
      draw();
      return hover;
    },
    dispose() { ro.disconnect(); },
  };
}

/** Everything three.js lives here, loaded lazily so it never touches the server render. */
async function createWebGlRenderer(canvas: HTMLCanvasElement, host: HTMLElement): Promise<PulsarRenderer> {
  const THREE = await import('three');
  const { Line2 } = await import('three/examples/jsm/lines/Line2.js');
  const { LineMaterial } = await import('three/examples/jsm/lines/LineMaterial.js');
  const { LineGeometry } = await import('three/examples/jsm/lines/LineGeometry.js');

  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: 'low-power' });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.setClearColor(0x000000, 0);

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(26, 1, 0.1, 100);
  const rig = new THREE.Group();
  scene.add(rig);

  const SAMPLES = 160;
  const WIDTH = 2.2;
  const SPACING = 0.027;
  const DEPTH = 0.012;
  const AMP = 0.42;
  const INK = new THREE.Color(0xf2f0ea);
  const RED = new THREE.Color(0xe5412f);

  const fill = new THREE.MeshBasicMaterial({ color: 0x0a0a0a, side: THREE.DoubleSide });
  let lines: { line: InstanceType<typeof Line2>; mat: InstanceType<typeof LineMaterial>; mesh: InstanceType<typeof THREE.Mesh>; y: number }[] = [];
  let count = 0;
  let hover: number | null = null;
  let scanStart = performance.now();
  let tiltX = 0, tiltY = 0, targetX = 0, targetY = 0;

  function build(weeks: PulsarWeek[]) {
    for (const l of lines) {
      rig.remove(l.line, l.mesh);
      l.line.geometry.dispose();
      l.mat.dispose();
      l.mesh.geometry.dispose();
    }
    lines = [];
    count = weeks.length || 53;
    const max = Math.max(1, ...weeks.flatMap((w) => w.days.map((d) => d.count)));
    for (let i = 0; i < count; i++) {
      // i = 0 is the oldest week: highest on screen and furthest back
      const ys = profile(weeks[i]?.days ?? null, max, SAMPLES);
      const baseY = (count / 2 - i) * SPACING;
      const z = -(count - i) * DEPTH;
      const pts: number[] = [];
      const shape = new THREE.Shape();
      shape.moveTo(-WIDTH / 2, baseY - 0.6);
      ys.forEach((y, s) => {
        const x = -WIDTH / 2 + (s / (SAMPLES - 1)) * WIDTH;
        pts.push(x, baseY + y * AMP, z);
        shape.lineTo(x, baseY + y * AMP);
      });
      shape.lineTo(WIDTH / 2, baseY - 0.6);
      const geo = new LineGeometry();
      geo.setPositions(pts);
      const mat = new LineMaterial({ color: INK.getHex(), linewidth: 1.25, transparent: true, opacity: 0.86 });
      const line = new Line2(geo, mat);
      const mesh = new THREE.Mesh(new THREE.ShapeGeometry(shape), fill);
      mesh.position.z = z - 0.001;
      line.renderOrder = i * 2 + 1;
      mesh.renderOrder = i * 2;
      rig.add(mesh, line);
      lines.push({ line, mat, mesh, y: baseY });
    }
    resize();
  }

  function resize() {
    const w = host.clientWidth, h = host.clientHeight;
    if (!w || !h) return;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    // fit the whole stack with a margin, whatever the aspect
    const span = count * SPACING + AMP + 0.2;
    // on wide stages the lines run longer (the quiet tails grow), up to 1.7x
    const stretch = Math.min(1.7, Math.max(1, (span * camera.aspect * 0.92) / WIDTH));
    rig.scale.x = stretch;
    const fitH = span / (2 * Math.tan((camera.fov * Math.PI) / 360));
    const fitW = (WIDTH * stretch + 0.2) / (2 * Math.tan((camera.fov * Math.PI) / 360) * camera.aspect);
    camera.position.set(0, -0.05, Math.max(fitH, fitW) + 1.2);
    camera.lookAt(0, 0, -1);
    camera.updateProjectionMatrix();
    for (const l of lines) l.mat.resolution.set(w, h);
    render();
  }

  function render(t = performance.now()) {
    tiltX += (targetX - tiltX) * 0.06;
    tiltY += (targetY - tiltY) * 0.06;
    rig.rotation.set(0.12 + tiltY, tiltX, 0);
    // a slow brightening that moves from the oldest week to the newest
    const scan = reduced ? -1 : (((t - scanStart) / 14000) % 1) * (count + 12) - 6;
    lines.forEach((l, i) => {
      const isHover = i === hover;
      const glow = Math.max(0, 1 - Math.abs(i - scan) / 3);
      l.mat.color.copy(isHover ? RED : INK);
      l.mat.linewidth = isHover ? 2.6 : 1.25;
      l.mat.opacity = isHover ? 1 : hover == null ? 0.62 + glow * 0.38 : 0.4;
    });
    renderer.render(scene, camera);
  }

  let raf = 0;
  let visible = true;
  const loop = (t: number) => {
    raf = requestAnimationFrame(loop);
    if (visible && document.visibilityState === 'visible') render(t);
  };
  if (!reduced) raf = requestAnimationFrame(loop);

  const io = new IntersectionObserver(([e]) => (visible = e.isIntersecting));
  io.observe(host);
  const ro = new ResizeObserver(resize);
  ro.observe(host);

  return {
    setData(weeks: PulsarWeek[]) {
      build(weeks);
      scanStart = performance.now();
    },
    pointer(cx: number | null, cy: number | null): number | null {
      if (cx == null || cy == null) {
        hover = null;
        targetX = targetY = 0;
        if (reduced) render();
        return null;
      }
      const r = host.getBoundingClientRect();
      const nx = ((cx - r.left) / r.width) * 2 - 1;
      const ny = ((cy - r.top) / r.height) * 2 - 1;
      if (!reduced) {
        targetX = nx * 0.14;
        targetY = ny * 0.06;
      }
      // nearest line baseline (projected) at or below the pointer
      let best: number | null = null, bestD = Infinity;
      const v = new THREE.Vector3();
      lines.forEach((l, i) => {
        rig.localToWorld(v.set(0, l.y, -(count - i) * DEPTH));
        v.project(camera);
        const sy = (1 - v.y) / 2 * r.height + r.top;
        const d = sy - cy;
        if (d >= -4 && d < bestD) { bestD = d; best = i; }
      });
      hover = bestD < 40 ? best : null;
      if (reduced) render();
      return hover;
    },
    dispose() {
      cancelAnimationFrame(raf);
      io.disconnect();
      ro.disconnect();
      renderer.dispose();
    },
  };
}
