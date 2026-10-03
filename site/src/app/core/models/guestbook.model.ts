export interface GuestbookEntry {
  id: string;
  seq: number;
  name: string;
  message: string;
  status: 'pending' | 'accepted';
  createdAt: string;
  likeCount: number;
  liked: boolean;
}

export interface LikeState {
  likeCount: number;
  liked: boolean;
}

export type SendState =
  | { kind: 'idle' }
  | { kind: 'sending' }
  | { kind: 'sent'; seq: number }
  | { kind: 'error'; message: string };
