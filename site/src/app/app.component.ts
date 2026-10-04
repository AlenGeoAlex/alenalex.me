import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LiveService } from '@core/services/live.service';
import { IndexRailComponent } from '@layout/index-rail/index-rail.component';
import { StatusBarComponent } from '@layout/status-bar/status-bar.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, IndexRailComponent, StatusBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.component.html',
  host: { class: 'grid min-h-dvh grid-cols-1 grid-rows-[auto_1fr_auto] md:grid-cols-[auto_minmax(0,1fr)] md:grid-rows-[1fr_auto]' },
})
export class AppComponent {
  constructor() {
    inject(LiveService).start();
  }
}
