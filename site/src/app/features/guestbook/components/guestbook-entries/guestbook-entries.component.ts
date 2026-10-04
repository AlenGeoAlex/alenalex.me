import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { GuestbookService } from '@core/services/guestbook.service';
import { RemoteService } from '@core/services/remote.service';
import { CatalogNoPipe } from '@shared/pipes/catalog-no.pipe';
import { OfflineNoticeComponent } from '@shared/components/offline-notice/offline-notice.component';
import { ReactionBadgesComponent } from '@shared/components/reactions/reaction-badges.component';
import { ReactionEffectsComponent } from '@shared/components/reactions/reaction-effects.component';

/** Guestbook notes. Pending notes are only visible to their author. Alen's reactions animate over a note while it's hovered. */
@Component({
  selector: 'app-guestbook-entries',
  imports: [CatalogNoPipe, OfflineNoticeComponent, ReactionBadgesComponent, ReactionEffectsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './guestbook-entries.component.html',
  host: { class: 'block' },
})
export class GuestbookEntriesComponent {
  protected readonly guestbook = inject(GuestbookService);
  private readonly remote = inject(RemoteService);

  /** 0 = all */
  readonly limit = input(0);

  /** the note under the pointer (or focus), whose reactions are playing */
  protected readonly hovered = signal<string | null>(null);

  protected readonly shown = computed(() => {
    const e = this.guestbook.entries();
    if (e.state !== 'ready') return [];
    return this.limit() ? e.data.slice(0, this.limit()) : e.data;
  });

  protected readonly error = computed(() => {
    const e = this.guestbook.entries();
    return e.state === 'offline' ? this.remote.describe(e.reason) : '';
  });
}
