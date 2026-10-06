import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, afterNextRender, afterRenderEffect, computed, effect, inject, input, resource, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ContentService } from '@core/services/content.service';
import { DiagramService } from '@core/services/diagram.service';
import { PostPreviewService } from '@core/services/post-preview.service';
import { Dialog } from '@angular/cdk/dialog';
import { ReaderThemeService } from '@core/services/reader-theme.service';
import { SeoService } from '@core/services/seo.service';
import { Post, PostSummary } from '@core/models/post.model';
import { CatalogNoPipe } from '@shared/pipes/catalog-no.pipe';
import { ParticleGlobeComponent } from '@shared/components/particle-globe/particle-globe.component';
import { TagChipComponent } from '@shared/components/tag-chip/tag-chip.component';
import { AiNoteComponent } from '@shared/components/ai-note/ai-note.component';
import { RevisionsDialogComponent, RevisionsDialogData } from '../components/revisions-dialog/revisions-dialog.component';
import { PostReferencesComponent } from '../components/post-references/post-references.component';

/**
 * A post. Published posts are prerendered from the repo at build time.
 * With ?preview=<sha|branch> the post is read from the repo at that ref (through the API) instead:
 * that's how old revisions and drafts are viewed.
 */
@Component({
  selector: 'app-post',
  imports: [RouterLink, CatalogNoPipe, ParticleGlobeComponent, TagChipComponent, AiNoteComponent, PostReferencesComponent],
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
  private readonly diagrams = inject(DiagramService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly destroyRef = inject(DestroyRef);
  private readonly body = viewChild<ElementRef<HTMLElement>>('body');
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
          revisionsSince: base?.revisionsSince ?? null,
        });
      }
      return this.summary() || this.draft() ? this.content.post(params.path) : null;
    },
  });

  /**
   * The post body is rendered from this repo's own markdown (at build time, or from a ref of the repo
   * for previews), so it's trusted. Angular's sanitizer would otherwise strip the heading ids the
   * contents links point at.
   */
  protected readonly html = computed<SafeHtml>(() => this.sanitizer.bypassSecurityTrustHtml(this.post.value()?.html ?? ''));

  /** The published parts of this post's series, in reading order (empty for standalone posts). */
  protected readonly seriesParts = computed<PostSummary[]>(() => {
    const series = this.post.value()?.series;
    return series ? this.content.partsOf(series.slug) : [];
  });

  /** In-page links (contents) keep ?preview, or they'd jump back to the published version. */
  protected readonly pageHref = computed(() => {
    const preview = this.preview();
    return `/writing/${this.path()}` + (preview ? `?preview=${encodeURIComponent(preview)}` : '');
  });

  /** the heading being read: the last one scrolled past the top band of the screen */
  protected readonly activeId = signal<string | null>(null);
  /** how far through the text, 0–1 */
  protected readonly progress = signal(0);
  /** the floating contents island (small screens) is expanded */
  protected readonly islandOpen = signal(false);
  protected readonly activeHeading = computed(() => {
    const headings = this.post.value()?.headings ?? [];
    const i = headings.findIndex((h) => h.id === this.activeId());
    return i >= 0 ? { ...headings[i], index: i + 1, total: headings.length } : null;
  });

  protected readonly globeColor = computed(() => (this.reader.theme() === 'light' ? '#171614' : '#f2f0ea'));

  /** The post HTML is inserted as-is, so its copy buttons are handled here, by delegation. */
  protected async onBodyClick(event: MouseEvent): Promise<void> {
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>('button.code-copy');
    const code = button?.closest('figure.code')?.querySelector('pre code');
    if (!button || !code) return;
    let copied = false;
    try {
      await navigator.clipboard.writeText(code.textContent ?? '');
      copied = true;
    } catch {
      // no clipboard permission (embedded browsers, some privacy settings): select the code and
      // try the old copy command; if that fails too, the selection is ready for ⌘C
      getSelection()?.selectAllChildren(code);
      copied = document.execCommand('copy');
      if (copied) getSelection()?.removeAllRanges();
    }
    button.textContent = copied ? 'copied ✓' : 'press ⌘C';
    button.setAttribute('aria-label', copied ? 'Copied' : 'Code selected, press Command C or Control C to copy');
    button.classList.add('is-done');
    setTimeout(() => {
      button.textContent = 'copy';
      button.setAttribute('aria-label', 'Copy code');
      button.classList.remove('is-done');
    }, 1600);
  }

  protected openRevisions(title: string): void {
    const folder = this.folder();
    if (!folder) return;
    this.dialog.open<void, RevisionsDialogData>(RevisionsDialogComponent, {
      data: { folder, path: this.path(), title, current: this.preview() ?? null, since: this.post.value()?.revisionsSince ?? null },
      ariaLabelledBy: 'revisions-title',
      backdropClass: ['bg-black/55', 'backdrop-blur-[2px]'],
      autoFocus: 'dialog',
    });
  }

  /** Where the reader is: which heading is above the reading line, and how much of the text is behind it. */
  private trackPosition(): void {
    const line = innerHeight * 0.3;
    let active: string | null = null;
    for (const h of this.post.value()?.headings ?? []) {
      const el = document.getElementById(h.id);
      if (!el || el.getBoundingClientRect().top > line) break;
      active = h.id;
    }
    this.activeId.set(active);
    const body = this.body()?.nativeElement.getBoundingClientRect();
    if (body) this.progress.set(Math.min(1, Math.max(0, (line - body.top) / body.height)));
  }

  constructor() {
    afterNextRender(() => {
      let frame = 0;
      const onScroll = () => (frame ||= requestAnimationFrame(() => ((frame = 0), this.trackPosition())));
      addEventListener('scroll', onScroll, { passive: true });
      addEventListener('resize', onScroll, { passive: true });
      this.destroyRef.onDestroy(() => {
        removeEventListener('scroll', onScroll);
        removeEventListener('resize', onScroll);
        cancelAnimationFrame(frame);
      });
    });
    // a new post (or revision) is in the DOM: start over
    afterRenderEffect(() => {
      if (!this.post.value()) return;
      this.islandOpen.set(false);
      this.trackPosition();
    });

    // runs in the browser only, after the post's HTML is in the DOM
    afterRenderEffect(() => {
      const body = this.body()?.nativeElement;
      const theme = this.reader.theme();
      if (body && this.post.value()) void this.diagrams.render(body, theme);
    });

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
