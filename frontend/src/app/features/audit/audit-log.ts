import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AUDIT_ACTIONS } from '../../core/api/api.models';
import { ComplianceApi } from '../../core/api/approvals.api';
import { EnumLabelPipe } from '../../shared/ui/enum-label';
import { TranslatePipe } from '@ngx-translate/core';

/** Read-only audit log for HR Admins and auditors: who viewed or changed sensitive data. */
@Component({
  selector: 'app-audit-log',
  imports: [DatePipe, MatFormFieldModule, MatInputModule, MatPaginatorModule, MatProgressBarModule, MatSelectModule, MatTableModule, RouterLink, TranslatePipe, EnumLabelPipe],
  template: `
    <h1>{{ 'audit.title' | translate }}</h1>
    <div class="filters">
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ 'audit.action' | translate }}</mat-label>
        <mat-select [value]="action()" (selectionChange)="action.set($event.value); page.set(0)" data-testid="action-filter">
          <mat-option [value]="null">{{ 'common.all' | translate }}</mat-option>
          @for (a of actions; track a) {
            <mat-option [value]="a">{{ a | enumLabel: 'audit' }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ 'audit.from' | translate }}</mat-label>
        <input matInput type="date" [value]="from()" (change)="from.set($any($event.target).value || null); page.set(0)" />
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ 'audit.to' | translate }}</mat-label>
        <input matInput type="date" [value]="to()" (change)="to.set($any($event.target).value || null); page.set(0)" />
      </mat-form-field>
    </div>

    @if (log.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (log.value(); as result) {
      <table mat-table [dataSource]="result.items" class="table">
        <ng-container matColumnDef="when">
          <th mat-header-cell *matHeaderCellDef>{{ 'audit.when' | translate }}</th>
          <td mat-cell *matCellDef="let e">{{ e.occurredAt | date: 'medium' }}</td>
        </ng-container>
        <ng-container matColumnDef="who">
          <th mat-header-cell *matHeaderCellDef>{{ 'audit.who' | translate }}</th>
          <td mat-cell *matCellDef="let e">{{ e.userName }}</td>
        </ng-container>
        <ng-container matColumnDef="action">
          <th mat-header-cell *matHeaderCellDef>{{ 'audit.action' | translate }}</th>
          <td mat-cell *matCellDef="let e">{{ e.action | enumLabel: 'audit' }}</td>
        </ng-container>
        <ng-container matColumnDef="summary">
          <th mat-header-cell *matHeaderCellDef>{{ 'audit.details' | translate }}</th>
          <td mat-cell *matCellDef="let e">
            @if (e.entityType === 'Ticket' && e.entityId) {
              <a [routerLink]="['/tickets', e.entityId]">{{ e.summary }}</a>
            } @else {
              {{ e.summary }}
            }
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns" data-testid="audit-row"></tr>
      </table>
      @if (result.totalCount === 0) {
        <p>{{ 'audit.empty' | translate }}</p>
      }
      <mat-paginator [length]="result.totalCount" [pageIndex]="page()" [pageSize]="50" (page)="onPage($event)" />
    }
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      margin-bottom: 16px;
    }
    .table {
      width: 100%;
    }
  `,
})
export class AuditLogPage {
  private readonly api = inject(ComplianceApi);

  protected readonly actions = AUDIT_ACTIONS;
  protected readonly columns = ['when', 'who', 'action', 'summary'];
  protected readonly action = signal<string | null>(null);
  protected readonly from = signal<string | null>(null);
  protected readonly to = signal<string | null>(null);
  protected readonly page = signal(0);

  protected readonly log = rxResource({
    params: () => ({
      action: this.action(),
      // Whole days in the browser's time zone; "to" includes the chosen day.
      from: this.from() ? new Date(`${this.from()}T00:00:00`).toISOString() : null,
      to: this.to() ? new Date(new Date(`${this.to()}T00:00:00`).getTime() + 86_400_000).toISOString() : null,
      page: this.page() + 1,
      pageSize: 50,
    }),
    stream: ({ params }) => this.api.auditLogs(params),
  });

  protected onPage(event: PageEvent): void {
    this.page.set(event.pageIndex);
  }
}
