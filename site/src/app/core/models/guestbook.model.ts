export interface GuestbookEntry {
  id: string;
  seq: number;
  name: string;
  message: string;
  status: 'pending' | 'accepted';
  createdAt: string;
  likeCount: number;
  liked: boolean;
  /** Alen's reactions from Discord, oldest first (see core/constants/reactions.constants.ts) */
  reactions: string[];
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
