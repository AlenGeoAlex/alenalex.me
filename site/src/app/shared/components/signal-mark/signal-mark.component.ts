import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type SignalHue = 'signal' | 'chrome' | 'cobalt' | 'bone';
export type SignalState = 'on' | 'off' | 'unknown';

const HUE_VAR: Record<SignalHue, string> = {
  signal: 'var(--color-signal)',
  chrome: 'var(--color-chrome)',
  cobalt: 'var(--color-cobalt)',
  bone: 'var(--color-bone)',
};

/**
 * Status mark. State is shown by shape, not just colour:
 * filled = on, outlined = off, hatched = unknown.
 */
@Component({
  selector: 'app-signal-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '',
  host: {
    class: 'inline-block h-[0.62em] w-[1.1em] shrink-0 border align-[0.05em]',
    'aria-hidden': 'true',
    '[style.border-color]': 'color()',
    '[style.background]': 'fill()',
  },
})
export class SignalMarkComponent {
  readonly hue = input<SignalHue>('bone');
  readonly state = input<SignalState>('on');

  protected readonly color = computed(() => HUE_VAR[this.hue()]);
  protected readonly fill = computed(() => {
    switch (this.state()) {
      case 'on':
        return this.color();
      case 'off':
        return 'transparent';
      default:
        return `repeating-linear-gradient(135deg, ${this.color()} 0 1px, transparent 1px 4px)`;
    }
  });
}
