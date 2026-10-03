import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, TimeoutError, catchError, firstValueFrom, throwError, timeout } from 'rxjs';
import { API_BASE_URL } from '@core/tokens/api-base-url.token';
import { ApiError, OfflineReason } from '@core/models/remote.model';

const TIMEOUT_MS = 8000;

/**
 * HTTP client shared by the API services: adds the base URL and a timeout,
 * and turns every failure into an ApiError.
 */
@Injectable({ providedIn: 'root' })
export class RemoteService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  get<T>(path: string): Promise<T> {
    return this.send(this.http.get<T>(this.baseUrl + path));
  }

  post<T>(path: string, body: unknown): Promise<T> {
    return this.send(this.http.post<T>(this.baseUrl + path, body));
  }

  delete<T>(path: string): Promise<T> {
    return this.send(this.http.delete<T>(this.baseUrl + path));
  }

  /** One-line error for the offline notice. Doesn't reveal where the API runs. */
  describe(reason: OfflineReason): string {
    switch (reason) {
      case 'unreachable':
        return 'connection refused · the server is touching grass';
      case 'timed out':
        return "timed out · it's thinking really hard";
      case 'warming up':
        return '503 · still warming up';
      default:
        return '500 · something tripped over a cable';
    }
  }

  private send<T>(request: Observable<T>): Promise<T> {
    return firstValueFrom(
      request.pipe(
        timeout(TIMEOUT_MS),
        catchError((err: unknown) => throwError(() => this.toApiError(err))),
      ),
    );
  }

  private toApiError(err: unknown): ApiError {
    if (err instanceof TimeoutError) return new ApiError(0, 'timed out');
    if (err instanceof HttpErrorResponse) {
      const message = (err.error as { error?: string } | null)?.error ?? err.statusText ?? 'error';
      return new ApiError(err.status, err.status === 0 ? 'unreachable' : message);
    }
    return new ApiError(0, 'unreachable');
  }
}
