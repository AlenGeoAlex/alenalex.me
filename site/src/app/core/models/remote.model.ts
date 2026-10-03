/**
 * State of data loaded from the API. The API can be offline, so that's a state
 * of its own rather than a spinner that never ends.
 */
export type RemoteState<T> =
  | { readonly state: 'loading' }
  | { readonly state: 'ready'; readonly data: T }
  | { readonly state: 'offline'; readonly reason: OfflineReason };

export type OfflineReason = 'unreachable' | 'timed out' | 'warming up' | 'error';

export const remoteLoading = <T>(): RemoteState<T> => ({ state: 'loading' });
export const remoteReady = <T>(data: T): RemoteState<T> => ({ state: 'ready', data });
export const remoteOffline = <T>(reason: OfflineReason): RemoteState<T> => ({ state: 'offline', reason });

/** A failed API call, normalised by RemoteService. status 0 = never reached the server. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }

  get reason(): OfflineReason {
    if (this.status === 0) return this.message === 'timed out' ? 'timed out' : 'unreachable';
    if (this.status === 503) return 'warming up';
    return 'error';
  }
}
