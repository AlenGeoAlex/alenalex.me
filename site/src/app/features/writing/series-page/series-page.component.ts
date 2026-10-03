import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ContentService } from '@core/services/content.service';
import { SeoService } from '@core/services/seo.service';
import { SeriesSummary } from '@core/models/post.model';
import { CatalogNoPipe } from '@shared/pipes/catalog-no.pipe';
import { TagChipComponent } from '@shared/components/tag-chip/tag-chip.component';
import { ParticleGlobeComponent } from '@shared/components/particle-globe/particle-globe.component';

/** A series: what it's about, and its parts as a timeline in reading order. */
@Component({
  selector: 'app-series-page',
  imports: [RouterLink, CatalogNoPipe, TagChipComponent, ParticleGlobeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './series-page.component.html',
})
export class SeriesPageComponent {
  readonly series = input.required<SeriesSummary>();

  private readonly content = inject(ContentService);
  private readonly seo = inject(SeoService);

  protected readonly parts = computed(() => this.content.partsOf(this.series().slug));
  protected readonly totalMinutes = computed(() => this.parts().reduce((sum, p) => sum + p.readingMinutes, 0));

  constructor() {
    effect(() => {
      const s = this.series();
      this.seo.set({
        title: `${s.title} · a series · Alen Alex`,
        description: s.description ?? `${s.title}: a series of posts by Alen Alex.`,
        path: `/writing/${s.slug}`,
        image: s.cover,
        noindex: s.draft,
      });
    });
  }
}
