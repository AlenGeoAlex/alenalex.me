import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { ContentService } from '@core/services/content.service';
import { PostComponent } from '../post/post.component';
import { SeriesPageComponent } from '../series-page/series-page.component';

/**
 * /writing/:slug is either a standalone post or a series page; /writing/:slug/:part is a part.
 * This route component decides which one to show.
 */
@Component({
  selector: 'app-writing-entry',
  imports: [PostComponent, SeriesPageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (series(); as series) {
      <app-series-page [series]="series" />
    } @else {
      <app-post [path]="path()" [preview]="preview()" />
    }
  `,
})
export class WritingEntryComponent {
  /** route params */
  readonly slug = input.required<string>();
  readonly part = input<string | undefined>();
  /** query param: a commit sha or branch to read the post at */
  readonly preview = input<string | undefined>();

  private readonly content = inject(ContentService);

  protected readonly series = computed(() => (this.part() ? undefined : this.content.seriesBySlug(this.slug())));
  protected readonly path = computed(() => (this.part() ? `${this.slug()}/${this.part()}` : this.slug()));
}
