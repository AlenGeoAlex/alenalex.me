import { Injectable, Injector, inject } from '@angular/core';
import type { DialogRef } from '@angular/cdk/dialog';
import type { SearchPaletteComponent } from './search-palette.component';

/**
 * Opens the ⌘K search palette (one at a time). The palette and the CDK dialog are loaded on the
 * first open, so they stay out of the initial bundle.
 */
@Injectable({ providedIn: 'root' })
export class SearchPaletteService {
  private readonly injector = inject(Injector);
  private ref: DialogRef<unknown, SearchPaletteComponent> | null = null;
  private opening = false;

  async open(): Promise<void> {
    if (this.ref || this.opening) return;
    this.opening = true;
    try {
      const [{ Dialog }, { Overlay }, { SearchPaletteComponent }] = await Promise.all([
        import('@angular/cdk/dialog'),
        import('@angular/cdk/overlay'),
        import('./search-palette.component'),
      ]);
      const overlay = this.injector.get(Overlay);
      const ref = this.injector.get(Dialog).open(SearchPaletteComponent, {
        ariaLabel: 'Search',
        autoFocus: 'first-tabbable',
        backdropClass: ['bg-black/55', 'backdrop-blur-[2px]'],
        // near the top, so results grow downwards without the box jumping
        positionStrategy: overlay.position().global().centerHorizontally().top('12vh'),
      });
      ref.closed.subscribe(() => (this.ref = null));
      this.ref = ref;
    } finally {
      this.opening = false;
    }
  }

  /**
   * ⌘K / Ctrl+K anywhere; "/" when not typing somewhere. On /writing, "/" goes to the page's own
   * search box instead.
   */
  handleKey(event: KeyboardEvent): void {
    const target = event.target as HTMLElement | null;
    const typing = !!target && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName));
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      void this.open();
    } else if (event.key === '/' && !typing && !event.metaKey && !event.ctrlKey && !event.altKey) {
      event.preventDefault();
      const inline = document.querySelector<HTMLInputElement>('[data-search-input]');
      if (inline) inline.focus();
      else void this.open();
    }
  }
}
