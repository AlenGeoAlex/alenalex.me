import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, afterNextRender, effect, inject, input, viewChild } from '@angular/core';
import { GlobeRenderer, createGlobeRenderer } from './globe-renderer';

/**
 * Decorative background: a slowly turning sphere of points with a pulse rolling over it.
 * Purely visual (aria-hidden), paused off-screen, a still frame under reduced motion.
 */
@Component({
  selector: 'app-particle-globe',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<canvas #canvas class="block size-full"></canvas>`,
  host: { class: 'pointer-events-none block', 'aria-hidden': 'true' },
})
export class ParticleGlobeComponent {
  /** colour of the resting points; the pulse is always signal red */
  readonly color = input('#f2f0ea');

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private renderer: GlobeRenderer | null = null;

  constructor() {
    const destroyRef = inject(DestroyRef);
    afterNextRender(async () => {
      try {
        this.renderer = await createGlobeRenderer(this.canvas().nativeElement, this.host.nativeElement, this.color());
        destroyRef.onDestroy(() => this.renderer?.dispose());
      } catch {
        // no WebGL: it's only decoration, so skip it
      }
    });
    effect(() => this.renderer?.setColor(this.color()));
  }
}
