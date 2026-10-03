import { Pipe, PipeTransform, inject } from '@angular/core';
import { ClockService } from '@core/services/clock.service';

/** iso | timeAgo: clock.now()  →  "3d ago". Pass the clock so it re-evaluates as time moves. */
@Pipe({ name: 'timeAgo' })
export class TimeAgoPipe implements PipeTransform {
  private readonly clock = inject(ClockService);

  transform(iso: string, _now?: Date | null): string {
    return this.clock.ago(iso);
  }
}
