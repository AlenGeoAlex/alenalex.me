import { Injectable, inject } from '@angular/core';
import { RemoteService } from './remote.service';
import { API_BASE_URL } from '@core/tokens/api-base-url.token';
import { PostRevision, PostSource } from '@core/models/post.model';

/**
 * /api/posts: a post's history and source, read by the API from the repo.
 * Powers the revisions modal and ?preview=<ref>.
 */
@Injectable({ providedIn: 'root' })
export class PostsApiService {
  private readonly remote = inject(RemoteService);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `folder` is the repo path under blogs/: "<folder>" or "<series>/<part>". */
  async revisions(folder: string): Promise<PostRevision[]> {
    const { revisions } = await this.remote.get<{ revisions: PostRevision[] }>(`/api/posts/${this.segments(folder)}/revisions`);
    return revisions;
  }

  source(folder: string, ref: string): Promise<PostSource> {
    return this.remote.get<PostSource>(`/api/posts/${this.segments(folder)}/source?ref=${encodeURIComponent(ref)}`);
  }

  /** Where an asset of the post lives at a ref (images in previews and drafts). */
  assetUrl(folder: string, file: string, ref: string): string {
    return `${this.baseUrl}/api/posts/${this.segments(folder)}/assets/${encodeURIComponent(file)}?ref=${encodeURIComponent(ref)}`;
  }

  private segments(folder: string): string {
    return folder.split('/').map(encodeURIComponent).join('/');
  }
}
