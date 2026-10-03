import { ChangeDetectionStrategy, Component } from '@angular/core';
import { SITE } from '@core/constants/site.constants';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';

@Component({
  selector: 'app-now',
  imports: [PageHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './now.component.html',
})
export class NowComponent {
  protected readonly site = SITE;
}
