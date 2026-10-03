import { Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { format, formatDistanceStrict, formatDuration } from 'date-fns';
import { TZDate, tzOffset } from '@date-fns/tz';
import { SITE } from '@core/constants/site.constants';

/**
 * One shared ticking clock, started only in the browser.
 * Shows Alen's time (Cork) next to the visitor's own, and the gap between them.
 */
@Injectable({ providedIn: 'root' })
export class ClockService {
  readonly now = signal<Date | null>(null);

  /** The visitor's IANA zone, e.g. "Asia/Kolkata". */
  readonly visitorZone = signal<string | null>(null);

  readonly corkTime = computed(() => this.timeIn(SITE.timeZone));
  readonly visitorTime = computed(() => this.timeIn(this.visitorZone()));

  /** "you're 4 hours 30 minutes ahead" / "you're 1 hour behind" / "same time as you" */
  readonly difference = computed(() => {
    const now = this.now();
    const zone = this.visitorZone();
    if (!now || !zone) return '';
    const minutes = tzOffset(zone, now) - tzOffset(SITE.timeZone, now);
    if (minutes === 0) return 'same time as you';
    const abs = Math.abs(minutes);
    const span = formatDuration({ hours: Math.floor(abs / 60), minutes: abs % 60 });
    return `you're ${span} ${minutes > 0 ? 'ahead' : 'behind'}`;
  });

  constructor() {
    if (isPlatformBrowser(inject(PLATFORM_ID))) {
      this.visitorZone.set(Intl.DateTimeFormat().resolvedOptions().timeZone);
      this.now.set(new Date());
      setInterval(() => this.now.set(new Date()), 1000);
    }
  }

  /** "3 days ago", relative to the ticking clock. */
  ago(iso: string): string {
    return formatDistanceStrict(new Date(iso), this.now() ?? new Date(), { addSuffix: true });
  }

  private timeIn(zone: string | null): string {
    const now = this.now();
    if (!now || !zone) return '--:--:--';
    return format(new TZDate(now, zone), 'HH:mm:ss');
  }
}
