import { InjectionToken, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Post } from '@core/models/post.model';

/** Loads a post by its URL path: "<slug>" or "<series-slug>/<part-slug>". */
export type PostLoader = (path: string) => Promise<Post | null>;

/**
 * Loads one post's rendered HTML. In the browser it's fetched from /content/posts;
 * at prerender time app.config.server.ts swaps in a loader that reads the file from disk.
 */
export const POST_LOADER = new InjectionToken<PostLoader>('POST_LOADER', {
  providedIn: 'root',
  factory: () => {
    const http = inject(HttpClient);
    return (path) => {
      const url = `/content/posts/${path.split('/').map(encodeURIComponent).join('/')}.json`;
      return firstValueFrom(http.get<Post>(url)).catch(() => null);
    };
  },
});
