import { ReactionEffect } from '@core/constants/reactions.constants';

/** One particle: where it starts, in px inside the note, and how it moves. */
interface Particle {
  size: number;
  left: number;
  top: number;
  frames: Keyframe[];
  duration: number;
  easing: string;
}

const rand = (min: number, max: number) => min + Math.random() * (max - min);
const pick = <T>(xs: readonly T[]) => xs[Math.floor(Math.random() * xs.length)];

/** Particle generators per effect, for a note of `w` × `h` px. */
export const EFFECTS: Record<ReactionEffect, (w: number, h: number) => Particle> = {
  float: (w, h) => {
    const sway = rand(10, 22) * pick([-1, 1]);
    const rise = h * rand(0.7, 1);
    return {
      size: rand(20, 30), left: rand(w * 0.55, w - 34), top: h - 26,
      duration: rand(1800, 2600), easing: 'cubic-bezier(.2,.6,.4,1)',
      frames: [
        { transform: 'translate(0,0) scale(.4) rotate(0deg)', opacity: 0 },
        { transform: `translate(${sway}px,${-rise * 0.3}px) scale(1) rotate(${sway / 2}deg)`, opacity: 1, offset: 0.25 },
        { transform: `translate(${-sway}px,${-rise * 0.65}px) scale(1) rotate(${-sway / 2}deg)`, opacity: 0.9, offset: 0.6 },
        { transform: `translate(${sway / 2}px,${-rise}px) scale(.8) rotate(0deg)`, opacity: 0 },
      ],
    };
  },
  rise: (w, h) => {
    const drift = rand(-14, 14);
    const rise = h * rand(0.45, 0.8);
    return {
      size: rand(14, 20), left: rand(w * 0.45, w - 26), top: h - 22,
      duration: rand(1100, 1600), easing: 'ease-out',
      frames: [
        { transform: 'translate(0,0) scale(.5)', opacity: 0 },
        { transform: `translate(${drift / 2}px,${-rise * 0.35}px) scale(1)`, opacity: 1, offset: 0.2 },
        { transform: `translate(${-drift / 3}px,${-rise * 0.6}px) scale(.9)`, opacity: 0.55, offset: 0.5 },
        { transform: `translate(${drift}px,${-rise * 0.8}px) scale(.85)`, opacity: 0.85, offset: 0.7 },
        { transform: `translate(${drift}px,${-rise}px) scale(.5)`, opacity: 0 },
      ],
    };
  },
  burst: (w, h) => {
    const spin = rand(90, 200) * pick([-1, 1]);
    const dx = rand(-16, 16);
    const dy = rand(-16, 4);
    return {
      size: rand(22, 30), left: rand(12, w - 36), top: rand(h * 0.45, h - 32),
      duration: rand(700, 950), easing: 'cubic-bezier(.2,.9,.3,1.2)',
      frames: [
        { transform: 'translate(0,0) scale(0) rotate(0deg)', opacity: 0 },
        { transform: `translate(${dx / 2}px,${dy / 2}px) scale(1.25) rotate(${spin / 2}deg)`, opacity: 1, offset: 0.35 },
        { transform: `translate(${dx}px,${dy}px) scale(0) rotate(${spin}deg)`, opacity: 0 },
      ],
    };
  },
  crawl: (w, h) => {
    const y = rand(h * 0.75, h - 24);
    const steps = 6;
    const frames: Keyframe[] = [];
    for (let i = 0; i <= steps; i++) {
      const t = i / steps;
      // a little side-to-side as it walks, facing the way it goes
      frames.push({
        transform: `translate(${t * (w + 40)}px,${(i % 2 ? -3 : 3)}px) rotate(${90 + (i % 2 ? -8 : 8)}deg)`,
        opacity: t === 0 || t === 1 ? 0 : 1,
      });
    }
    return { size: rand(18, 22), left: -24, top: y, duration: rand(2600, 3600), easing: 'linear', frames };
  },
  bounce: (w, h) => {
    const hop = h * rand(0.35, 0.55);
    const dx = rand(60, 120) * pick([-1, 1]);
    return {
      size: rand(22, 28), left: rand(w * 0.25, w * 0.75), top: h - 30,
      duration: rand(1200, 1500), easing: 'linear',
      frames: [
        { transform: 'translate(0,0) rotate(0deg) scale(.6)', opacity: 0 },
        { transform: `translate(${dx * 0.25}px,${-hop}px) rotate(${dx > 0 ? 90 : -90}deg) scale(1)`, opacity: 1, offset: 0.25, easing: 'ease-in' },
        { transform: `translate(${dx * 0.5}px,0) rotate(${dx > 0 ? 180 : -180}deg) scale(1,.85)`, opacity: 1, offset: 0.5, easing: 'ease-out' },
        { transform: `translate(${dx * 0.75}px,${-hop * 0.5}px) rotate(${dx > 0 ? 270 : -270}deg) scale(1)`, opacity: 1, offset: 0.75, easing: 'ease-in' },
        { transform: `translate(${dx}px,0) rotate(${dx > 0 ? 360 : -360}deg) scale(.6)`, opacity: 0 },
      ],
    };
  },
  peek: (w, h) => ({
    size: rand(22, 28), left: rand(12, w - 36), top: rand(h * 0.45, h - 32),
    duration: rand(1300, 1700), easing: 'ease-in-out',
    frames: [
      { transform: 'scale(0)', opacity: 0 },
      { transform: 'scale(1.1)', opacity: 1, offset: 0.15 },
      { transform: 'scale(1)', opacity: 1, offset: 0.4 },
      // blink
      { transform: 'scale(1,.1)', opacity: 1, offset: 0.48 },
      { transform: 'scale(1)', opacity: 1, offset: 0.56 },
      { transform: 'scale(1)', opacity: 1, offset: 0.85 },
      { transform: 'scale(0)', opacity: 0 },
    ],
  }),
};

/** Adds one particle of `src` to `layer` and removes it when its animation ends. */
export function spawn(layer: HTMLElement, src: string, effect: ReactionEffect): void {
  const p = EFFECTS[effect](layer.clientWidth, layer.clientHeight);
  const img = document.createElement('img');
  img.src = src;
  img.alt = '';
  img.decoding = 'async';
  img.style.cssText =
    `position:absolute;left:${p.left}px;top:${p.top}px;width:${p.size}px;height:${p.size}px;` +
    'opacity:0;will-change:transform,opacity;filter:drop-shadow(0 2px 3px rgb(0 0 0 / .5))';
  layer.append(img);
  img.animate(p.frames, { duration: p.duration, easing: p.easing, fill: 'forwards' }).finished
    .catch(() => undefined)
    .finally(() => img.remove());
}
