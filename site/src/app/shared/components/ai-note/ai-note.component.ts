import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Says whether AI helped write a post (`ai-assist:` in its .meta): an icon, with the
 * explanation in a tooltip on hover or focus.
 */
@Component({
  selector: 'app-ai-note',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span tabindex="0" aria-describedby="ai-note-tip"
      class="group relative inline-flex items-center gap-1.5 text-(--r-ink-2) outline-offset-4 hover:text-(--r-ink) focus-visible:text-(--r-ink)">
      @if (assisted()) {
        <svg viewBox="0 0 16 16" aria-hidden="true" class="size-4 fill-none stroke-current stroke-[1.2]">
          <path d="M7 1.5c.4 3 1.5 4.1 4.5 4.5-3 .4-4.1 1.5-4.5 4.5-.4-3-1.5-4.1-4.5-4.5 3-.4 4.1-1.5 4.5-4.5Z" />
          <path d="M12.5 10.5c.2 1.3.7 1.8 2 2-1.3.2-1.8.7-2 2-.2-1.3-.7-1.8-2-2 1.3-.2 1.8-.7 2-2Z" />
        </svg>
        <span>ai-assisted</span>
      } @else {
        <svg viewBox="0 0 16 16" aria-hidden="true" class="size-4 fill-none stroke-current stroke-[1.2]">
          <path d="M10.5 2.5 13.5 5.5 5.5 13.5 2 14l.5-3.5Z" />
          <path d="M9 4l3 3" />
        </svg>
        <span>no ai</span>
      }
      <span id="ai-note-tip" role="tooltip"
        class="pointer-events-none absolute bottom-full left-1/2 z-10 mb-2 w-max max-w-[min(17rem,80vw)] -translate-x-1/2 translate-y-1 border border-(--r-rule) bg-(--r-bg) px-3 py-2 text-left text-xs leading-relaxed text-(--r-ink-2) opacity-0 shadow-[0_12px_30px_-12px_rgb(0_0_0/0.6)] transition duration-150 group-hover:translate-y-0 group-hover:opacity-100 group-focus-visible:translate-y-0 group-focus-visible:opacity-100">
        @if (assisted()) {
          AI was used in some capacity while writing this post.
        } @else {
          Written without AI. No AI was used to write this post.
        }
      </span>
    </span>
  `,
})
export class AiNoteComponent {
  /** `ai-assist:` from the post's .meta */
  readonly assisted = input.required<boolean>();
}
