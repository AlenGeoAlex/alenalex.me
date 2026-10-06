import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { DialogRef } from '@angular/cdk/dialog';
import { Router } from '@angular/router';
import { SearchService, markMatches } from '@core/services/search.service';
import { SearchHit } from '@core/models/search.model';

const DEBOUNCE_MS = 120;

/** ⌘K: search posts and series from anywhere. Opened by SearchPaletteService. */
@Component({
  selector: 'app-search-palette',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './search-palette.component.html',
})
export class SearchPaletteComponent {
  private readonly dialog = inject(DialogRef);
  private readonly router = inject(Router);
  protected readonly search = inject(SearchService);

  protected readonly query = signal('');
  protected readonly hits = signal<SearchHit[] | null>(null);
  protected readonly active = signal(0);
  protected readonly mark = markMatches;

  /** posts and parts first, then series; the keyboard walks this order */
  protected readonly groups = computed(() => {
    const hits = this.hits() ?? [];
    return [
      { label: 'posts', hits: hits.filter((h) => h.kind !== 'series') },
      { label: 'series', hits: hits.filter((h) => h.kind === 'series') },
    ].filter((g) => g.hits.length);
  });
  protected readonly ordered = computed(() => this.groups().flatMap((g) => g.hits));

  constructor() {
    this.search.warmUp();
    let timer: ReturnType<typeof setTimeout> | undefined;
    effect(() => {
      const q = this.query().trim();
      clearTimeout(timer);
      if (!q) {
        this.hits.set(null);
        return;
      }
      timer = setTimeout(async () => {
        const hits = await this.search.search(q, 10);
        if (hits) {
          this.hits.set(hits);
          this.active.set(0);
        }
      }, DEBOUNCE_MS);
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(timer));
  }

  protected indexOf(hit: SearchHit): number {
    return this.ordered().indexOf(hit);
  }

  protected onKeydown(event: KeyboardEvent): void {
    const n = this.ordered().length;
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (n) this.active.update((i) => (i + (event.key === 'ArrowDown' ? 1 : n - 1)) % n);
      document.getElementById(`search-hit-${this.active()}`)?.scrollIntoView({ block: 'nearest' });
    } else if (event.key === 'Enter') {
      const hit = this.ordered()[this.active()];
      if (hit) {
        event.preventDefault();
        this.open(hit);
      }
    }
  }

  protected open(hit: SearchHit): void {
    this.dialog.close();
    void this.router.navigateByUrl(hit.path);
  }

  protected close(): void {
    this.dialog.close();
  }
}
