import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { humanize } from './labels';

/** Translation groups for API enum values ("status.InProgress", "category.Payroll", …). */
export type EnumGroup = 'status' | 'priority' | 'category' | 'role' | 'sla' | 'decision' | 'strategy' | 'audit' | 'day' | 'event';

/** The translated label of an enum value, or the humanized value when no translation exists. */
export function enumLabel(translate: TranslateService, group: EnumGroup, value: string | null | undefined): string {
  if (!value) return '';
  const key = `${group}.${value}`;
  const translated = translate.instant(key) as string;
  return translated === key ? humanize(value) : translated;
}

/**
 * `{{ ticket.status | enumLabel: 'status' }}`. Pure: the language is fixed for the page's life
 * (changing it reloads), and its texts are loaded before the first render.
 */
@Pipe({ name: 'enumLabel' })
export class EnumLabelPipe implements PipeTransform {
  private readonly translate = inject(TranslateService);

  transform(value: string | null | undefined, group: EnumGroup): string {
    return enumLabel(this.translate, group, value);
  }
}
