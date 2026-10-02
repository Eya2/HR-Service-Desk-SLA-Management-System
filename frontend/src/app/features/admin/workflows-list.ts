import { Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { WorkflowInfo } from '../../core/api/api.models';
import { WorkflowsApi } from '../../core/api/approvals.api';
import { humanize } from '../../shared/ui/labels';

@Component({
  selector: 'app-workflows-list',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, MatTableModule, RouterLink],
  template: `
    <h1>Approval workflows</h1>
    <p class="intro">Each request type can require an ordered chain of approvals before HR starts working on it.</p>
    @if (workflows.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (workflows.value(); as list) {
      <table mat-table [dataSource]="list" class="table">
        <ng-container matColumnDef="type">
          <th mat-header-cell *matHeaderCellDef>Request type</th>
          <td mat-cell *matCellDef="let w">
            {{ w.requestTypeName }}
            @if (w.requestTypeIsConfidential) {
              <mat-icon class="lock" fontSet="material-symbols-outlined" aria-label="Confidential">lock</mat-icon>
            }
          </td>
        </ng-container>
        <ng-container matColumnDef="steps">
          <th mat-header-cell *matHeaderCellDef>Approval chain</th>
          <td mat-cell *matCellDef="let w" [attr.data-testid]="'chain-' + w.requestTypeName">{{ chain(w) }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let w">
            <a mat-button [routerLink]="[w.requestTypeId]">{{ w.isConfigured ? 'Edit' : 'Add' }}</a>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
    }
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
    .intro {
      color: var(--mat-sys-on-surface-variant);
    }
    .table {
      width: 100%;
    }
    .lock {
      font-size: 16px;
      width: 16px;
      height: 16px;
      vertical-align: middle;
      color: var(--mat-sys-error);
    }
  `,
})
export class WorkflowsList {
  private readonly api = inject(WorkflowsApi);

  protected readonly columns = ['type', 'steps', 'actions'];
  protected readonly workflows = rxResource({ stream: () => this.api.list() });

  protected chain(workflow: WorkflowInfo): string {
    if (!workflow.isConfigured) return 'No approval needed';
    const steps = workflow.steps.map((s) => humanize(s.approverRole)).join(' → ');
    return workflow.isActive ? steps : `${steps} (disabled)`;
  }
}
