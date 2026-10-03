import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { GuestbookService } from '@core/services/guestbook.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { GuestbookComposerComponent } from '@features/guestbook/components/guestbook-composer/guestbook-composer.component';
import { GuestbookEntriesComponent } from '@features/guestbook/components/guestbook-entries/guestbook-entries.component';

@Component({
  selector: 'app-guestbook-page',
  imports: [PageHeaderComponent, GuestbookComposerComponent, GuestbookEntriesComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './guestbook-page.component.html',
})
export class GuestbookPageComponent {
  constructor() {
    inject(GuestbookService).load();
  }
}
