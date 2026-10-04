import { Injectable, inject } from '@angular/core';
import { RemoteService } from './remote.service';
import { LiveStatus } from '@core/models/status.model';

/** /api/status: Discord presence, what's playing and recently played, service health. */
@Injectable({ providedIn: 'root' })
export class StatusApiService {
  private readonly remote = inject(RemoteService);

  async current(): Promise<LiveStatus> {
    const status = await this.remote.get<LiveStatus>('/api/status');
    // an API from before the listening history doesn't send it
    return { ...status, recentTracks: status.recentTracks ?? [] };
  }
}
