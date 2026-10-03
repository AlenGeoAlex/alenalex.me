import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { GuestbookService } from '@core/services/guestbook.service';
import { GUESTBOOK_MAX_CHARS, GUESTBOOK_MIN_CHARS } from '@core/constants/guestbook.constants';
import { CatalogNoPipe } from '@shared/pipes/catalog-no.pipe';

/** Guestbook form. A note gets its number as soon as it's posted. */
@Component({
  selector: 'app-guestbook-composer',
  imports: [CatalogNoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './guestbook-composer.component.html',
  host: { class: 'block' },
})
export class GuestbookComposerComponent {
  protected readonly guestbook = inject(GuestbookService);
  protected readonly max = GUESTBOOK_MAX_CHARS;
  protected readonly text = signal('');

  protected readonly valid = computed(() => this.guestbook.isValid(this.text()));
  protected readonly hint = computed(() => {
    const n = this.text().trim().length;
    if (n === 0) return `${GUESTBOOK_MIN_CHARS}–${GUESTBOOK_MAX_CHARS} characters · shows after a quick moderation`;
    if (n < GUESTBOOK_MIN_CHARS) return `${GUESTBOOK_MIN_CHARS - n} more to go`;
    return `${GUESTBOOK_MAX_CHARS - this.text().length} left · ⌘↵ to post`;
  });

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.valid()) return;
    if (await this.guestbook.post(this.text())) this.text.set('');
  }
}
