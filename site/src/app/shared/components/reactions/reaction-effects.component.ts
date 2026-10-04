import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, PLATFORM_ID, computed, effect, inject, input } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { reactionIcon, reactionsFor } from '@core/constants/reactions.constants';
import { spawn } from './reaction-effects';

/** How often a new particle starts while the note is hovered, and how many can be on screen at once. */
const EVERY_MS = 240;
const MAX_LIVE = 14;

/**
 * The hover effect for a note's reactions: while `active`, each reaction takes turns sending its
 * object across the note (hearts float up, bugs crawl along…). Covers its positioned parent plus
 * some room above it, and never takes pointer events. Does nothing for people who prefer reduced motion.
 */
@Component({
  selector: 'app-reaction-effects',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '',
  // reaches 5rem above the note so floating things have room to drift up over the one before it
  host: { class: 'pointer-events-none absolute inset-x-0 -top-20 bottom-0 z-10 overflow-hidden', 'aria-hidden': 'true' },
})
export class ReactionEffectsComponent {
  readonly keys = input.required<readonly string[]>();
  readonly active = input(false);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly reactions = computed(() => reactionsFor(this.keys()));
  private timer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    effect(() => {
      const reactions = this.reactions();
      const on = this.active() && reactions.length > 0 && this.browser && !matchMedia('(prefers-reduced-motion: reduce)').matches;
      this.stop();
      if (!on) return;
      let turn = 0;
      const tick = () => {
        if (this.host.childElementCount >= MAX_LIVE) return;
        const r = reactions[turn++ % reactions.length];
        spawn(this.host, reactionIcon(r.key), r.effect);
      };
      tick();
      this.timer = setInterval(tick, EVERY_MS);
    });
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  private stop(): void {
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
  }
}
