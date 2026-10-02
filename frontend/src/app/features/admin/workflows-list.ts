import { Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { WorkflowInfo } from '../../core/api/api.models';
import { WorkflowsApi } from '../../core/api/approvals.api';
import { enumLabel } from '../../shared/ui/enum-label';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-workflows-list',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, MatTableModule, RouterLink, TranslatePipe],
  template: `
    <h1>{{ 'workflows.title' | translate }}</h1>
    <p class="intro">{{ 'workflows.intro' | translate }}</p>
    @if (workflows.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (workflows.value(); as list) {
      <table mat-table [dataSource]="list" class="table">
        <ng-container matColumnDef="type">
          <th mat-header-cell *matHeaderCellDef>{{ 'workflows.requestType' | translate }}</th>
          <td mat-cell *matCellDef="let w">
            {{ w.requestTypeName }}
            @if (w.requestTypeIsConfidential) {
              <mat-icon class="lock" fontSet="material-symbols-outlined" [attr.aria-label]="'ticket.confidential' | translate">lock</mat-icon>
            }
          </td>
        </ng-container>
        <ng-container matColumnDef="steps">
          <th mat-header-cell *matHeaderCellDef>{{ 'workflows.chain' | translate }}</th>
          <td mat-cell *matCellDef="let w" [attr.data-testid]="'chain-' + w.requestTypeName">{{ chain(w) }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let w">
            <a mat-button [routerLink]="[w.requestTypeId]">{{ (w.isConfigured ? 'common.edit' : 'common.add') | translate }}</a>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
    }
  `,
  styles: `
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
  private readonly translate = inject(TranslateService);

  protected readonly columns = ['type', 'steps', 'actions'];
  protected readonly workflows = rxResource({ stream: () => this.api.list() });

  protected chain(workflow: WorkflowInfo): string {
    if (!workflow.isConfigured) return this.translate.instant('workflows.noApproval');
    const steps = workflow.steps.map((s) => enumLabel(this.translate, 'role', s.approverRole)).join(' → ');
    return workflow.isActive ? steps : this.translate.instant('workflows.disabled', { steps });
  }
}
