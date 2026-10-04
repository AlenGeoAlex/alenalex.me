import { Injectable, inject } from '@angular/core';
import { RemoteService } from './remote.service';
import { GuestbookEntry, LikeState } from '@core/models/guestbook.model';

/** /api/guestbook: list, sign, like. */
@Injectable({ providedIn: 'root' })
export class GuestbookApiService {
  private readonly remote = inject(RemoteService);

  async list(): Promise<GuestbookEntry[]> {
    const { entries } = await this.remote.get<{ entries: GuestbookEntry[] }>('/api/guestbook');
    return entries.map(withReactions);
  }

  async create(name: string, message: string): Promise<GuestbookEntry> {
    const { entry } = await this.remote.post<{ entry: GuestbookEntry }>('/api/guestbook', { name, message });
    return withReactions(entry);
  }

  like(id: string): Promise<LikeState> {
    return this.remote.post<LikeState>(`/api/guestbook/${encodeURIComponent(id)}/likes`, null);
  }

  unlike(id: string): Promise<LikeState> {
    return this.remote.delete<LikeState>(`/api/guestbook/${encodeURIComponent(id)}/likes`);
  }
}

/** An API from before reactions doesn't send them. */
const withReactions = (entry: GuestbookEntry): GuestbookEntry => ({ ...entry, reactions: entry.reactions ?? [] });
