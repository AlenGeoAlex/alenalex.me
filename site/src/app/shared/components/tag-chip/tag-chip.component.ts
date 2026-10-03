import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Border colours a tag can get. */
const HUES = ['#e5412f', '#f2c230', '#2f6fe8', '#3fb37f', '#b06ee8', '#e8833a'];

/**
 * A tag as a small chip. Its border colour looks random but is derived from the tag text,
 * so a tag keeps the same colour everywhere and across reloads.
 */
@Component({
  selector: 'app-tag-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ng-content />`,
  host: {
    class: 'inline-flex items-center border px-2 py-0.5 text-xs leading-5',
    '[style.border-color]': 'hue()',
  },
})
export class TagChipComponent {
  readonly tag = input.required<string>();

  protected readonly hue = computed(() => {
    let hash = 0;
    for (const ch of this.tag()) hash = (hash * 31 + ch.charCodeAt(0)) | 0;
    return HUES[Math.abs(hash) % HUES.length];
  });
}
