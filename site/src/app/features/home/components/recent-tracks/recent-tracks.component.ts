import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { ClockService } from '@core/services/clock.service';
import { LiveStatus, RecentTrack } from '@core/models/status.model';

/** The last few songs, with album art. Shown in a card over the "listening" row of the live panel. */
@Component({
  selector: 'app-recent-tracks',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="caps m-0 mb-3">recently played</p>
    @if (tracks().length) {
      <ol class="m-0 grid list-none gap-2.5 p-0">
        @for (t of tracks(); track t.playedAt + t.track; let first = $first) {
          <li class="grid grid-cols-[2.5rem_minmax(0,1fr)] items-center gap-3">
            @if (t.artUrl) {
              <img [src]="t.artUrl" [alt]="t.album ? 'Album art for ' + t.album : ''" width="40" height="40"
                loading="lazy" decoding="async" referrerpolicy="no-referrer"
                class="size-10 border border-rule object-cover" />
            } @else {
              <span class="grid size-10 place-items-center border border-rule text-ink-3" aria-hidden="true">♪</span>
            }
            <span class="min-w-0">
              <span class="block truncate text-sm" [class]="first && isPlaying(t) ? 'text-ink' : 'text-ink-2'">{{ t.track }}</span>
              <span class="block truncate text-xs text-ink-3">
                {{ t.artist }} ·
                @if (first && isPlaying(t)) { <span class="text-signal">now</span> } @else { {{ clock.ago(t.playedAt) }} }
              </span>
            </span>
          </li>
        }
      </ol>
    } @else {
      <p class="m-0 text-xs text-ink-2">nothing played lately.</p>
    }
  `,
  host: { class: 'block' },
})
export class RecentTracksComponent {
  readonly tracks = input.required<RecentTrack[]>();
  readonly playing = input<LiveStatus['spotify']>(null);

  protected readonly clock = inject(ClockService);

  protected isPlaying(t: RecentTrack): boolean {
    const now = this.playing();
    return !!now && now.track === t.track && now.artist === t.artist;
  }
}
