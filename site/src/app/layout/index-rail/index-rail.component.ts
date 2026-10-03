import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { SECTIONS } from '@core/constants/sections.constants';
import { SectionObjectComponent } from '@shared/components/section-object/section-object.component';

/** Site nav: a rail on the left on desktop, a strip across the top on small screens. */
@Component({
  selector: 'app-index-rail',
  imports: [RouterLink, RouterLinkActive, SectionObjectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './index-rail.component.html',
  host: { class: 'contents' },
})
export class IndexRailComponent {
  protected readonly sections = SECTIONS;
}
