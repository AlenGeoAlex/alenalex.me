import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ContributionWeek } from '@core/models/github.model';
import { PulsarRenderer, PulsarWeek, createPulsarRenderer } from './pulsar-renderer';

/**
 * One line per week of the last year of GitHub contributions, oldest at the back,
 * newest at the front. The cursor tilts it; hovering (or tapping) a line shows that week.
 * With no data (loading / offline) the lines are flat.
 */
@Component({
  selector: 'app-pulsar-plot',
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pulsar-plot.component.html',
  host: { class: 'block' },
})
export class PulsarPlotComponent {
  /** null while loading or offline: the plot then shows flat lines. */
  readonly weeks = input<ContributionWeek[] | null>(null);
  readonly state = input<'loading' | 'ready' | 'offline'>('loading');
  readonly total = input<number | null>(null);

  protected readonly selected = signal<PulsarWeek | null>(null);

  protected readonly data = computed<PulsarWeek[]>(() =>
    (this.weeks() ?? []).map((w) => ({ days: w.days, total: w.days.reduce((sum, d) => sum + d.count, 0) })),
  );

  protected readonly busiest = computed(() =>
    this.data().reduce<PulsarWeek | null>((best, w) => (!best || w.total > best.total ? w : best), null),
  );

  protected readonly yearTotal = computed(() => this.total() ?? this.data().reduce((sum, w) => sum + w.total, 0));

  private readonly stage = viewChild.required<ElementRef<HTMLDivElement>>('stage');
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private renderer: PulsarRenderer | null = null;

  constructor() {
    const destroyRef = inject(DestroyRef);
    afterNextRender(async () => {
      this.renderer = await createPulsarRenderer(this.canvas().nativeElement, this.stage().nativeElement);
      this.renderer.setData(this.data());
      destroyRef.onDestroy(() => this.renderer?.dispose());
    });
    effect(() => this.renderer?.setData(this.data()));
  }

  protected onPointer(event: MouseEvent): void {
    const i = this.renderer?.pointer(event.clientX, event.clientY) ?? null;
    this.selected.set(i === null ? null : (this.data()[i] ?? null));
  }

  protected onLeave(): void {
    this.renderer?.pointer(null, null);
    this.selected.set(null);
  }
}
