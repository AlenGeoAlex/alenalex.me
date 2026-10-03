import { Injectable, inject } from '@angular/core';
import { RemoteService } from './remote.service';
import { GithubSummary } from '@core/models/github.model';

/** /api/github: repos, stars and the contribution calendar, cached by the API. */
@Injectable({ providedIn: 'root' })
export class GithubApiService {
  private readonly remote = inject(RemoteService);

  summary(): Promise<GithubSummary> {
    return this.remote.get<GithubSummary>('/api/github');
  }
}
