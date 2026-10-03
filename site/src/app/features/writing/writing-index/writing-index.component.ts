import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ContentService } from '@core/services/content.service';
import { CatalogNoPipe } from '@shared/pipes/catalog-no.pipe';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';

@Component({
  selector: 'app-writing-index',
  imports: [RouterLink, CatalogNoPipe, PageHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './writing-index.component.html',
})
export class WritingIndexComponent {
  private readonly content = inject(ContentService);
  /** every published post and part (for the catalog count) */
  protected readonly posts = this.content.posts;
  /** standalone posts and series, newest activity first */
  protected readonly entries = this.content.entries();
}
