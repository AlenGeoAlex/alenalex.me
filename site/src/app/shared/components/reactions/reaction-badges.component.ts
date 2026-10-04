import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { reactionIcon, reactionsFor } from '@core/constants/reactions.constants';

/** Alen's reactions on a note, resting: small 3D objects that tilt towards the pointer when the note is hovered. */
@Component({
  selector: 'app-reaction-badges',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (reactions().length) {
      <ul class="m-0 flex list-none items-center gap-1 p-0 perspective-[300px]" [attr.aria-label]="label()">
        @for (r of reactions(); track r.key; let i = $index) {
          <li class="grid size-7 place-items-center" [title]="'Alen reacted with ' + r.label">
            <img [src]="icon(r.key)" alt="" width="28" height="28" loading="lazy" decoding="async"
              class="size-7 drop-shadow-[0_3px_4px_rgb(0_0_0/0.55)] transition-transform duration-500 ease-out-expo
                     group-hover/note:-translate-y-0.5 group-hover/note:transform-[rotateY(-18deg)_rotateX(10deg)_scale(1.12)]"
              [style.transition-delay.ms]="i * 40" />
          </li>
        }
      </ul>
    }
  `,
  host: { class: 'contents' },
})
export class ReactionBadgesComponent {
  readonly keys = input.required<readonly string[]>();

  protected readonly reactions = computed(() => reactionsFor(this.keys()));
  protected readonly label = computed(() => 'Alen reacted with ' + this.reactions().map((r) => r.label).join(', '));
  protected readonly icon = reactionIcon;
}
