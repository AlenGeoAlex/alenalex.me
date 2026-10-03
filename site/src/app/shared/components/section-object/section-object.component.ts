import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Section image rendered by tools/objects. Decorative only. */
@Component({
  selector: 'app-section-object',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './section-object.component.html',
  host: {
    class: 'inline-block shrink-0 leading-none',
    'aria-hidden': 'true',
    '[style.width.px]': 'size()',
  },
})
export class SectionObjectComponent {
  readonly name = input.required<string>();
  readonly size = input(48);
}
