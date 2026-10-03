import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { LiveService } from '@core/services/live.service';
import { ClockService } from '@core/services/clock.service';
import { RemoteService } from '@core/services/remote.service';
import { SITE } from '@core/constants/site.constants';
import { Repo } from '@core/models/github.model';
import { CatalogNoPipe } from '@shared/pipes/catalog-no.pipe';
import { TimeAgoPipe } from '@shared/pipes/time-ago.pipe';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { OfflineNoticeComponent } from '@shared/components/offline-notice/offline-notice.component';
import { SignalHue, SignalMarkComponent } from '@shared/components/signal-mark/signal-mark.component';

const LANGUAGE_HUE: Record<string, SignalHue> = {
  Rust: 'signal',
  'C#': 'cobalt',
  TypeScript: 'cobalt',
  Java: 'chrome',
  JavaScript: 'chrome',
};

interface NumberedRepo {
  no: number;
  repo: Repo;
  hue: SignalHue;
}

@Component({
  selector: 'app-work',
  imports: [CatalogNoPipe, TimeAgoPipe, PageHeaderComponent, OfflineNoticeComponent, SignalMarkComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work.component.html',
})
export class WorkComponent {
  protected readonly live = inject(LiveService);
  protected readonly clock = inject(ClockService);
  private readonly remote = inject(RemoteService);
  protected readonly site = SITE;

  /** Own repos, newest push first, numbered RP 001… in the order they were started. */
  protected readonly repos = computed<NumberedRepo[]>(() => {
    const github = this.live.github();
    if (github.state !== 'ready') return [];
    const own = github.data.repos.filter((r) => !r.fork);
    const order = [...own].sort((a, b) => a.createdAt.localeCompare(b.createdAt));
    return own.map((repo) => ({
      repo,
      no: order.indexOf(repo) + 1,
      hue: (repo.language && LANGUAGE_HUE[repo.language]) || 'bone',
    }));
  });

  protected readonly error = computed(() => {
    const github = this.live.github();
    return github.state === 'offline' ? this.remote.describe(github.reason) : '';
  });
}
