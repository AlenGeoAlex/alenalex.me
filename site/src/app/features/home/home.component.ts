import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LiveService } from '@core/services/live.service';
import { ContentService } from '@core/services/content.service';
import { GuestbookService } from '@core/services/guestbook.service';
import { SITE } from '@core/constants/site.constants';
import { CatalogNoPipe } from '@shared/pipes/catalog-no.pipe';
import { PulsarPlotComponent } from '@shared/components/pulsar-plot/pulsar-plot.component';
import { LivePanelComponent } from '@features/home/components/live-panel/live-panel.component';
import { GuestbookComposerComponent } from '@features/guestbook/components/guestbook-composer/guestbook-composer.component';
import { GuestbookEntriesComponent } from '@features/guestbook/components/guestbook-entries/guestbook-entries.component';

interface BootLine {
  tag: string;
  text: string;
  warn: boolean;
}

@Component({
  selector: 'app-home',
  imports: [RouterLink, CatalogNoPipe, PulsarPlotComponent, LivePanelComponent, GuestbookComposerComponent, GuestbookEntriesComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.component.html',
})
export class HomeComponent {
  private readonly live = inject(LiveService);
  private readonly content = inject(ContentService);
  protected readonly guestbook = inject(GuestbookService);
  protected readonly site = SITE;

  protected readonly latest = this.content.posts.slice(0, 2);
  protected readonly postCount = this.content.posts.length;

  protected readonly plot = computed(() => {
    const github = this.live.github();
    return github.state === 'ready'
      ? { weeks: github.data.contributions.weeks, total: github.data.contributions.total, state: github.state }
      : { weeks: null, total: null, state: github.state };
  });

  /** The homelab line of the boot log, from live status. */
  protected readonly servicesLine = computed<BootLine>(() => {
    const status = this.live.status();
    if (status.state === 'loading') return { tag: '..', text: 'homelab: checking', warn: false };
    if (status.state === 'offline') return { tag: '--', text: 'homelab: status unknown', warn: false };
    const { up, total } = status.data.homelab;
    if (total === 0) return { tag: '..', text: 'homelab: no services reported', warn: false };
    return up === total
      ? { tag: 'OK', text: `homelab: ${total} service${total === 1 ? '' : 's'} running · all up`, warn: false }
      : { tag: 'WARN', text: `homelab: ${up}/${total} services up`, warn: true };
  });

  constructor() {
    this.guestbook.load();
  }
}
