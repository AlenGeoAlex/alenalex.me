import { Injectable, inject } from '@angular/core';
import { RemoteService } from './remote.service';
import { LiveStatus } from '@core/models/status.model';

/** /api/status: Discord presence, what's playing, service health. */
@Injectable({ providedIn: 'root' })
export class StatusApiService {
  private readonly remote = inject(RemoteService);

  current(): Promise<LiveStatus> {
    return this.remote.get<LiveStatus>('/api/status');
  }
}
