import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { SectionObjectComponent } from '@shared/components/section-object/section-object.component';

/** Page heading: name, catalog number, section image and the command line above it. */
@Component({
  selector: 'app-page-header',
  imports: [SectionObjectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './page-header.component.html',
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly no = input.required<string>();
  readonly command = input.required<string>();
  readonly object = input.required<string>();

  /** Words of the command, so it only ever wraps between words, never at "--". */
  protected readonly words = computed(() => this.command().split(' '));
}
