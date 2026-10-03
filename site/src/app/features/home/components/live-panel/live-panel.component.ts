import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { LiveService } from '@core/services/live.service';
import { ClockService } from '@core/services/clock.service';
import { SignalHue, SignalMarkComponent, SignalState } from '@shared/components/signal-mark/signal-mark.component';

interface LiveRow {
  key: string;
  value: string;
  detail?: string | null;
  state: SignalState;
  hue: SignalHue;
}

/** Live state: the time in Cork next to yours, presence, music, services, last push. */
@Component({
  selector: 'app-live-panel',
  imports: [SignalMarkComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './live-panel.component.html',
  host: { class: 'block' },
})
export class LivePanelComponent {
  private readonly live = inject(LiveService);
  protected readonly clock = inject(ClockService);

  protected readonly rows = computed<LiveRow[]>(() => {
    const status = this.live.status();
    const github = this.live.github();
    this.clock.now(); // re-evaluate "last push" as time passes
    const rows: LiveRow[] = [];

    if (status.state === 'ready') {
      const { discord, spotify, homelab } = status.data;
      rows.push({
        key: 'discord', hue: 'cobalt',
        state: discord.status === 'unknown' ? 'unknown' : discord.status === 'offline' ? 'off' : 'on',
        value: discord.status === 'dnd' ? 'do not disturb' : discord.status,
        detail: discord.activity,
      });
      rows.push(spotify
        ? { key: 'listening', hue: 'signal', state: 'on', value: spotify.track, detail: spotify.artist }
        : { key: 'listening', hue: 'signal', state: 'off', value: 'silence' });
      rows.push(homelab.total === 0
        ? { key: 'homelab', hue: 'chrome', state: 'unknown', value: 'no services reported' }
        : {
            key: 'homelab', hue: 'chrome',
            state: homelab.up === homelab.total ? 'on' : homelab.up === 0 ? 'off' : 'unknown',
            value: `${homelab.up}/${homelab.total} services up`,
          });
    } else {
      const value = status.state === 'loading' ? 'checking…' : 'unknown';
      rows.push(
        { key: 'discord', hue: 'cobalt', state: 'unknown', value },
        { key: 'listening', hue: 'signal', state: 'unknown', value },
        { key: 'homelab', hue: 'chrome', state: 'unknown', value: status.state === 'loading' ? value : 'status unknown' },
      );
    }

    rows.push(github.state === 'ready'
      ? {
          key: 'last push', hue: 'bone', state: 'on',
          value: github.data.lastPushAt ? this.clock.ago(github.data.lastPushAt) : 'never',
          detail: `${github.data.publicRepos} public repos · ${github.data.totalStars} stars`,
        }
      : { key: 'last push', hue: 'bone', state: 'unknown', value: github.state === 'loading' ? 'checking…' : 'unknown' });
    return rows;
  });
}
