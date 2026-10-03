import { Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { GithubApiService } from './github-api.service';
import { StatusApiService } from './status-api.service';
import { GithubSummary } from '@core/models/github.model';
import { LiveStatus } from '@core/models/status.model';
import { ApiError, RemoteState, remoteLoading, remoteOffline, remoteReady } from '@core/models/remote.model';

const STATUS_EVERY_MS = 60_000;
const GITHUB_EVERY_MS = 10 * 60_000;

/**
 * Live system state, polled while the tab is visible.
 * On the server (prerender) nothing is fetched: pages ship in their loading state.
 */
@Injectable({ providedIn: 'root' })
export class LiveService {
  private readonly githubApi = inject(GithubApiService);
  private readonly statusApi = inject(StatusApiService);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly github = signal<RemoteState<GithubSummary>>(remoteLoading());
  readonly status = signal<RemoteState<LiveStatus>>(remoteLoading());

  /** true once any call succeeded, false when every call failed, null while we don't know yet. */
  readonly apiUp = computed<boolean | null>(() => {
    const states = [this.github().state, this.status().state];
    if (states.includes('ready')) return true;
    if (states.every((s) => s === 'offline')) return false;
    return null;
  });

  private started = false;

  start(): void {
    if (!this.browser || this.started) return;
    this.started = true;
    this.poll(() => this.loadStatus(), STATUS_EVERY_MS);
    this.poll(() => this.loadGithub(), GITHUB_EVERY_MS);
  }

  retryGithub(): void {
    this.github.set(remoteLoading());
    void this.loadGithub();
  }

  private async loadGithub(): Promise<void> {
    try {
      this.github.set(remoteReady(await this.githubApi.summary()));
    } catch (e) {
      // keep the last good value if there is one
      if (this.github().state !== 'ready') this.github.set(remoteOffline((e as ApiError).reason));
    }
  }

  private async loadStatus(): Promise<void> {
    try {
      this.status.set(remoteReady(await this.statusApi.current()));
    } catch (e) {
      this.status.set(remoteOffline((e as ApiError).reason));
    }
  }

  private poll(load: () => Promise<void>, everyMs: number): void {
    void load();
    setInterval(() => {
      if (document.visibilityState === 'visible') void load();
    }, everyMs);
  }
}
