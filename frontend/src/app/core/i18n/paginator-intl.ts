import { Injectable, inject } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { TranslateService } from '@ngx-translate/core';

/** Paginator labels in the interface language. */
@Injectable()
export class TranslatedPaginatorIntl extends MatPaginatorIntl {
  private readonly translate = inject(TranslateService);

  constructor() {
    super();
    const t = (key: string) => this.translate.instant(`paginator.${key}`) as string;
    this.itemsPerPageLabel = t('itemsPerPage');
    this.nextPageLabel = t('next');
    this.previousPageLabel = t('previous');
    this.firstPageLabel = t('first');
    this.lastPageLabel = t('last');
    this.getRangeLabel = (page, pageSize, length) => {
      if (length === 0 || pageSize === 0) return this.translate.instant('paginator.empty', { length }) as string;
      const start = page * pageSize + 1;
      const end = Math.min(length, (page + 1) * pageSize);
      return this.translate.instant('paginator.range', { start, end, length }) as string;
    };
  }
}
