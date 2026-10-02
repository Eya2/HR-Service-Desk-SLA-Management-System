import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { SlaStateName } from '../../core/api/api.models';

const LABELS: Record<SlaStateName, string> = { None: 'No SLA', OnTrack: 'On track', AtRisk: 'At risk', Breached: 'Breached' };
const ICONS: Record<SlaStateName, string> = { None: 'remove', OnTrack: 'schedule', AtRisk: 'warning', Breached: 'alarm' };

/** SLA state with its deadline, or "paused" when the clock is stopped. */
@Component({
  selector: 'app-sla-badge',
  imports: [DatePipe, MatIconModule, MatTooltipModule],
  template: `
    <span class="badge" [attr.data-state]="state()" [matTooltip]="tooltip()">
      <mat-icon fontSet="material-symbols-outlined">{{ paused() ? 'pause_circle' : icon() }}</mat-icon>
      {{ paused() ? 'Paused' : label() }}
      @if (dueAt() && !paused() && state() !== 'None') {
        <span class="due">· {{ dueAt() | date: 'short' }}</span>
      }
    </span>
  `,
  styles: `
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 8px;
      border-radius: 12px;
      font: var(--mat-sys-label-medium);
      white-space: nowrap;
      background: var(--mat-sys-surface-container-high);
    }
    .badge mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .badge[data-state='OnTrack'] {
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .badge[data-state='AtRisk'] {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    .badge[data-state='Breached'] {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .due {
      font-weight: 400;
    }
  `,
})
export class SlaBadge {
  readonly state = input.required<SlaStateName>();
  readonly dueAt = input<string | null>(null);
  readonly paused = input(false);

  protected readonly label = computed(() => LABELS[this.state()] ?? this.state());
  protected readonly icon = computed(() => ICONS[this.state()] ?? 'schedule');
  protected readonly tooltip = computed(() =>
    this.paused() ? 'The SLA clock is paused (waiting for approval, for the employee, or resolved).' : 'Resolution deadline in business hours',
  );
}
