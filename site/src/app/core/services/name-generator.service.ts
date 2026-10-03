import { Injectable } from '@angular/core';

const ADJECTIVES = ['anxious', 'sleepy', 'caffeinated', 'recursive', 'async', 'stochastic', 'idempotent', 'fuzzy',
  'latent', 'verbose', 'silent', 'cursed', 'haunted', 'elegant', 'chaotic', 'stubborn', 'pensive'];
const NOUNS = ['raccoon', 'kernel', 'daemon', 'socket', 'pointer', 'mutex', 'goblin', 'wizard', 'buffer',
  'cron', 'packet', 'thread', 'shard', 'replica', 'artifact', 'phantom', 'bard'];

/** Random guestbook names like "idempotent-goblin". */
@Injectable({ providedIn: 'root' })
export class NameGeneratorService {
  next(): string {
    return `${this.pick(ADJECTIVES)}-${this.pick(NOUNS)}`;
  }

  private pick<T>(xs: readonly T[]): T {
    return xs[Math.floor(Math.random() * xs.length)];
  }
}
