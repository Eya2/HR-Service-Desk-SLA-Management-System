import { Component, computed, inject, input } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { enumLabel } from './enum-label';

const PRIORITIES = new Set(['Low', 'Medium', 'High', 'Critical']);

/** A coloured pill for a case status or priority. */
@Component({
  selector: 'app-status-chip',
  template: `<span class="chip" [attr.data-tone]="tone()">{{ label() }}</span>`,
  styles: `
    .chip {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 12px;
      font: var(--mat-sys-label-medium);
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface);
      white-space: nowrap;
    }
    .chip[data-tone='info'] {
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .chip[data-tone='warn'] {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    .chip[data-tone='danger'] {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
  `,
})
export class StatusChip {
  readonly value = input.required<string>();

  private readonly translate = inject(TranslateService);

  /** Statuses and priorities share this chip. */
  protected readonly label = computed(() =>
    PRIORITIES.has(this.value()) ? enumLabel(this.translate, 'priority', this.value()) : enumLabel(this.translate, 'status', this.value()),
  );
  protected readonly tone = computed(() => {
    switch (this.value()) {
      case 'New':
      case 'Open':
      case 'InProgress':
      case 'Reopened':
        return 'info';
      case 'PendingApproval':
      case 'WaitingOnEmployee':
      case 'High':
        return 'warn';
      case 'Rejected':
      case 'Critical':
        return 'danger';
      default:
        return 'neutral';
    }
  });
}
