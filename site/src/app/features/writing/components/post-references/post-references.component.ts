import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PostReference } from '@core/models/post.model';

/**
 * The numbered list of sources at the end of a post. The superscript markers in the text link
 * to #ref-<n> here.
 */
@Component({
  selector: 'app-post-references',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="references-title" class="mt-14 border border-(--r-rule)">
      <header class="flex items-baseline justify-between border-b border-(--r-rule) px-4 py-2.5">
        <h2 id="references-title" class="caps m-0 font-normal text-(--r-ink-3)">references</h2>
        <span class="text-xs text-(--r-ink-3)">{{ references().length }}</span>
      </header>
      <ol class="m-0 list-none p-0">
        @for (ref of references(); track ref.url; let i = $index) {
          <li [id]="'ref-' + (i + 1)"
            class="scroll-mt-24 border-b border-(--r-rule) last:border-b-0 target:bg-(--r-glass) target:[&_.ref-no]:text-(--r-accent)">
            <a [href]="ref.url" target="_blank" rel="noopener"
              class="group grid grid-cols-[2.25rem_minmax(0,1fr)_auto] items-baseline gap-3 px-4 py-3 text-sm no-underline">
              <span class="ref-no cat text-[0.75rem] text-(--r-ink-3) group-hover:text-(--r-accent)">{{ (i + 1).toString().padStart(2, '0') }}</span>
              <span class="min-w-0 truncate text-(--r-ink-2) group-hover:text-(--r-ink) group-hover:underline">{{ ref.title }}</span>
              <span class="flex items-baseline gap-2 text-xs text-(--r-ink-3)">
                <span class="hidden sm:inline">{{ ref.domain }}</span>
                <span aria-hidden="true" class="transition-transform duration-200 group-hover:translate-x-0.5 group-hover:-translate-y-0.5">↗</span>
              </span>
            </a>
          </li>
        }
      </ol>
    </section>
  `,
})
export class PostReferencesComponent {
  readonly references = input.required<PostReference[]>();
}
