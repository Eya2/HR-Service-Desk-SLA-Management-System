import { Component, computed, input } from '@angular/core';
import { humanize } from './labels';

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

  protected readonly label = computed(() => humanize(this.value()));
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
