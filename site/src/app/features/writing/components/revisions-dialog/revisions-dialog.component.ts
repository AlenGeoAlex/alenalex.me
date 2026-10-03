import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Router } from '@angular/router';
import { OfflineNoticeComponent } from '@shared/components/offline-notice/offline-notice.component';
import { PostsApiService } from '@core/services/posts-api.service';
import { RemoteService } from '@core/services/remote.service';
import { ReaderThemeService } from '@core/services/reader-theme.service';
import { PostRevision } from '@core/models/post.model';
import { ApiError, RemoteState, remoteLoading, remoteOffline, remoteReady } from '@core/models/remote.model';

export interface RevisionsDialogData {
  folder: string;
  /** URL path under /writing/ */
  path: string;
  title: string;
  /** the ref being read right now, if previewing */
  current: string | null;
  /** `revisions-since:` of the post: older commits aren't listed */
  since: string | null;
}

/** The commits that changed a post's text, loaded from the API when the modal opens. Picking one opens that version. */
@Component({
  selector: 'app-revisions-dialog',
  imports: [OfflineNoticeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './revisions-dialog.component.html',
})
export class RevisionsDialogComponent {
  protected readonly data = inject<RevisionsDialogData>(DIALOG_DATA);
  private readonly dialogRef = inject(DialogRef);
  private readonly api = inject(PostsApiService);
  private readonly remote = inject(RemoteService);
  private readonly router = inject(Router);
  protected readonly reader = inject(ReaderThemeService);

  protected readonly revisions = signal<RemoteState<PostRevision[]>>(remoteLoading());

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.revisions.set(remoteLoading());
    try {
      this.revisions.set(remoteReady(await this.api.revisions(this.data.folder, this.data.since)));
    } catch (e) {
      this.revisions.set(remoteOffline((e as ApiError).reason));
    }
  }

  protected describe(reason: Parameters<RemoteService['describe']>[0]): string {
    return this.remote.describe(reason);
  }

  /** The published version is "current" when no older ref is being read. */
  protected readonly readingPublished = !this.data.current;

  protected isCurrent(rev: PostRevision): boolean {
    return !!this.data.current && rev.sha.startsWith(this.data.current);
  }

  protected open(ref: string | null): void {
    this.dialogRef.close();
    void this.router.navigate(['/writing', ...this.data.path.split('/')], { queryParams: ref ? { preview: ref } : {} });
  }

  protected close(): void {
    this.dialogRef.close();
  }
}
