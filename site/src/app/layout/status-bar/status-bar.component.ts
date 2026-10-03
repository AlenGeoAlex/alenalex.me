import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { LiveService } from '@core/services/live.service';
import { ClockService } from '@core/services/clock.service';
import { SITE } from '@core/constants/site.constants';

/** Footer on every page: live data status, the time in Cork, and links. */
@Component({
  selector: 'app-status-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './status-bar.component.html',
})
export class StatusBarComponent {
  protected readonly live = inject(LiveService);
  protected readonly clock = inject(ClockService);
  protected readonly site = SITE;
}
