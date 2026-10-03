import { ChangeDetectionStrategy, Component, computed, effect, inject, input, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ContentService } from '@core/services/content.service';
import { PostPreviewService } from '@core/services/post-preview.service';
import { Dialog } from '@angular/cdk/dialog';
import { ReaderThemeService } from '@core/services/reader-theme.service';
import { SeoService } from '@core/services/seo.service';
import { Post, PostSummary } from '@core/models/post.model';
import { CatalogNoPipe } from '@shared/pipes/catalog-no.pipe';
import { ParticleGlobeComponent } from '@shared/components/particle-globe/particle-globe.component';
import { TagChipComponent } from '@shared/components/tag-chip/tag-chip.component';
import { RevisionsDialogComponent, RevisionsDialogData } from '../components/revisions-dialog/revisions-dialog.component';

/**
 * A post. Published posts are prerendered from the repo at build time.
 * With ?preview=<sha|branch> the post is read from the repo at that ref (through the API) instead:
 * that's how old revisions and drafts are viewed.
 */
@Component({
  selector: 'app-post',
  imports: [RouterLink, CatalogNoPipe, ParticleGlobeComponent, TagChipComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './post.component.html',
})
export class PostComponent {
  /** URL path under /writing/: "<slug>" or "<series-slug>/<part-slug>" */
  readonly path = input.required<string>();
  /** query param: a commit sha or branch to read the post at */
  readonly preview = input<string | undefined>();

  private readonly content = inject(ContentService);
  private readonly previews = inject(PostPreviewService);
  private readonly dialog = inject(Dialog);
  private readonly seo = inject(SeoService);
  protected readonly reader = inject(ReaderThemeService);

  protected readonly summary = computed(() => this.content.summary(this.path()));
  protected readonly draft = computed(() => this.content.draft(this.path()));
  protected readonly folder = computed(() => this.summary()?.folder ?? this.draft()?.folder ?? null);
  protected readonly neighbours = computed(() => this.content.neighbours(this.path()));

  protected readonly post = resource<Post | null, { path: string; preview: string | undefined }>({
    params: () => ({ path: this.path(), preview: this.preview() }),
    loader: async ({ params }) => {
      const folder = this.folder();
      if (params.preview && folder) {
        // the series context comes from the published build (or the draft's own JSON)
        const base = this.summary() ?? (await this.content.post(params.path));
        return this.previews.postAt(folder, params.preview, {
          no: base?.no ?? 0,
          path: params.path,
          series: base?.series ?? null,
          partIndex: base?.partIndex ?? null,
          partCount: base?.partCount ?? null,
        });
      }
      return this.summary() || this.draft() ? this.content.post(params.path) : null;
    },
  });

  /** The published parts of this post's series, in reading order (empty for standalone posts). */
  protected readonly seriesParts = computed<PostSummary[]>(() => {
    const series = this.post.value()?.series;
    return series ? this.content.partsOf(series.slug) : [];
  });

  protected readonly globeColor = computed(() => (this.reader.theme() === 'light' ? '#171614' : '#f2f0ea'));

  protected openRevisions(title: string): void {
    const folder = this.folder();
    if (!folder) return;
    this.dialog.open<void, RevisionsDialogData>(RevisionsDialogComponent, {
      data: { folder, path: this.path(), title, current: this.preview() ?? null },
      ariaLabelledBy: 'revisions-title',
      backdropClass: ['bg-black/55', 'backdrop-blur-[2px]'],
      autoFocus: 'dialog',
    });
  }

  constructor() {
    effect(() => {
      const post = this.post.value();
      if (post && !this.preview()) {
        this.seo.set({
          title: post.pageTitle ?? `${post.title} · Alen Alex`,
          description: post.excerpt ?? `${post.title}, a post by Alen Alex.`,
          path: `/writing/${post.path}`,
          image: post.ogImage,
          type: 'article',
          published: post.date,
          noindex: !!post.draft,
        });
      } else if (this.post.status() === 'resolved' && !post) {
        this.seo.set({ title: 'not in the catalog · Alen Alex', description: 'This post does not exist.', path: `/writing/${this.path()}` });
      }
    });
  }
}
