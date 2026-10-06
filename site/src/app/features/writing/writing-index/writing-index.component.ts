import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, linkedSignal, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ContentService } from '@core/services/content.service';
import { SearchService, markMatches } from '@core/services/search.service';
import { SearchHit } from '@core/models/search.model';
import { CatalogNoPipe } from '@shared/pipes/catalog-no.pipe';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';

/** How long typing has to pause before searching. */
const DEBOUNCE_MS = 140;

@Component({
  selector: 'app-writing-index',
  imports: [RouterLink, CatalogNoPipe, PageHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './writing-index.component.html',
})
export class WritingIndexComponent {
  private readonly content = inject(ContentService);
  private readonly router = inject(Router);
  protected readonly search = inject(SearchService);

  /** every published post and part (for the catalog count) */
  protected readonly posts = this.content.posts;
  /** standalone posts and series, newest activity first */
  protected readonly entries = this.content.entries();

  /** ?q= in the URL, so a search can be shared */
  readonly q = input<string | undefined>();
  protected readonly query = linkedSignal(() => this.q() ?? '');
  /** null while not searching: the full list shows */
  protected readonly hits = signal<SearchHit[] | null>(null);
  protected readonly mark = markMatches;

  constructor() {
    let timer: ReturnType<typeof setTimeout> | undefined;
    effect(() => {
      const q = this.query().trim();
      clearTimeout(timer);
      if (!q) {
        this.hits.set(null);
        return;
      }
      timer = setTimeout(async () => {
        const hits = await this.search.search(q, 20);
        if (hits) this.hits.set(hits);
      }, DEBOUNCE_MS);
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(timer));
  }

  protected onInput(value: string): void {
    this.query.set(value);
    void this.router.navigate([], { queryParams: { q: value.trim() || null }, replaceUrl: true });
  }
}
