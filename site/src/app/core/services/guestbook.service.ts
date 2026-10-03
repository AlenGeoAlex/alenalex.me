import { Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { GuestbookApiService } from './guestbook-api.service';
import { NameGeneratorService } from './name-generator.service';
import { GUESTBOOK_MAX_CHARS, GUESTBOOK_MIN_CHARS } from '@core/constants/guestbook.constants';
import { GuestbookEntry, SendState } from '@core/models/guestbook.model';
import { ApiError, RemoteState, remoteLoading, remoteOffline, remoteReady } from '@core/models/remote.model';

/** Guestbook state: anyone can leave a note; it shows as pending until moderated in Discord. */
@Injectable({ providedIn: 'root' })
export class GuestbookService {
  private readonly api = inject(GuestbookApiService);
  private readonly names = inject(NameGeneratorService);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly entries = signal<RemoteState<GuestbookEntry[]>>(remoteLoading());
  readonly name = signal('visitor');
  readonly send = signal<SendState>({ kind: 'idle' });
  readonly acceptedCount = computed(() => {
    const e = this.entries();
    return e.state === 'ready' ? e.data.filter((x) => x.status === 'accepted').length : null;
  });

  private loaded = false;

  load(force = false): void {
    if (!this.browser || (this.loaded && !force)) return;
    if (!this.loaded) this.name.set(this.names.next());
    this.loaded = true;
    if (force) this.entries.set(remoteLoading());
    this.api
      .list()
      .then((entries) => this.entries.set(remoteReady(entries)))
      .catch((e: ApiError) => this.entries.set(remoteOffline(e.reason)));
  }

  reroll(): void {
    this.name.set(this.names.next());
  }

  isValid(message: string): boolean {
    const n = message.trim().length;
    return n >= GUESTBOOK_MIN_CHARS && n <= GUESTBOOK_MAX_CHARS;
  }

  async post(message: string): Promise<boolean> {
    if (!this.isValid(message)) return false;
    this.send.set({ kind: 'sending' });
    try {
      const entry = await this.api.create(this.name(), message.trim());
      this.entries.update((e) => remoteReady(e.state === 'ready' ? [entry, ...e.data] : [entry]));
      this.send.set({ kind: 'sent', seq: entry.seq });
      return true;
    } catch (e) {
      this.send.set({ kind: 'error', message: this.sendError(e as ApiError) });
      return false;
    }
  }

  async toggleLike(entry: GuestbookEntry): Promise<void> {
    const liked = !entry.liked;
    // optimistic, then reconcile with the server's count
    this.patch(entry.id, { liked, likeCount: entry.likeCount + (liked ? 1 : -1) });
    try {
      this.patch(entry.id, await (liked ? this.api.like(entry.id) : this.api.unlike(entry.id)));
    } catch {
      this.patch(entry.id, { liked: entry.liked, likeCount: entry.likeCount });
    }
  }

  private sendError(err: ApiError): string {
    if (err.status === 429) return 'too many notes from you this hour. try again later.';
    if (err.status === 422) return err.message;
    if (err.status === 0) return "couldn't reach the server, so your note wasn't saved. try again in a bit.";
    return `couldn't save your note (${err.message}).`;
  }

  private patch(id: string, changes: Partial<GuestbookEntry>): void {
    this.entries.update((e) =>
      e.state === 'ready' ? remoteReady(e.data.map((x) => (x.id === id ? { ...x, ...changes } : x))) : e,
    );
  }
}
