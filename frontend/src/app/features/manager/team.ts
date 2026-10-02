import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { ApprovalsApi } from '../../core/api/approvals.api';
import { humanize } from '../../shared/ui/labels';
import { StatusChip } from '../../shared/ui/status-chip';

/** The manager's direct reports: key figures and their requests (confidential cases excluded). */
@Component({
  selector: 'app-team',
  imports: [DatePipe, MatCardModule, MatPaginatorModule, MatTableModule, RouterLink, StatusChip],
  template: `
    <h1>My team</h1>
    @if (stats.value(); as s) {
      <div class="tiles">
        <mat-card appearance="outlined"><mat-card-content><span class="value" data-testid="team-size">{{ s.teamSize }}</span><span class="label">Direct reports</span></mat-card-content></mat-card>
        <mat-card appearance="outlined"><mat-card-content><span class="value" data-testid="open-cases">{{ s.openCases }}</span><span class="label">Open requests</span></mat-card-content></mat-card>
        <mat-card appearance="outlined"><mat-card-content><span class="value">{{ s.submittedLast30Days }}</span><span class="label">Submitted in 30 days</span></mat-card-content></mat-card>
        <mat-card appearance="outlined"><mat-card-content><span class="value">{{ s.pendingMyApproval }}</span><span class="label">Waiting for my approval</span></mat-card-content></mat-card>
      </div>
      @if (s.byStatus.length > 0) {
        <p class="breakdown">
          @for (b of s.byStatus; track b.status) {
            <span>{{ humanize(b.status) }}: <strong>{{ b.count }}</strong></span>
          }
        </p>
      }
    }

    @if (tickets.value(); as page) {
      @if (page.totalCount === 0) {
        <p>Your team has no requests yet.</p>
      } @else {
        <table mat-table [dataSource]="page.items" class="table">
          <ng-container matColumnDef="reference">
            <th mat-header-cell *matHeaderCellDef>Reference</th>
            <td mat-cell *matCellDef="let t"><a [routerLink]="['/tickets', t.id]">{{ t.reference }}</a></td>
          </ng-container>
          <ng-container matColumnDef="requester">
            <th mat-header-cell *matHeaderCellDef>Employee</th>
            <td mat-cell *matCellDef="let t">{{ t.requesterName }}</td>
          </ng-container>
          <ng-container matColumnDef="type">
            <th mat-header-cell *matHeaderCellDef>Request</th>
            <td mat-cell *matCellDef="let t">{{ t.requestTypeName }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let t"><app-status-chip [value]="t.status" /></td>
          </ng-container>
          <ng-container matColumnDef="created">
            <th mat-header-cell *matHeaderCellDef>Submitted</th>
            <td mat-cell *matCellDef="let t">{{ t.createdAt | date: 'mediumDate' }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
        </table>
        <mat-paginator [length]="page.totalCount" [pageIndex]="pageIndex()" [pageSize]="20" (page)="onPage($event)" />
      }
    }
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
    .tiles {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: 12px;
    }
    .tiles mat-card-content {
      display: grid;
    }
    .value {
      font: var(--mat-sys-display-small);
      color: var(--mat-sys-primary);
    }
    .label {
      color: var(--mat-sys-on-surface-variant);
    }
    .breakdown {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      color: var(--mat-sys-on-surface-variant);
    }
    .table {
      width: 100%;
    }
  `,
})
export class Team {
  private readonly api = inject(ApprovalsApi);

  protected readonly humanize = humanize;
  protected readonly columns = ['reference', 'requester', 'type', 'status', 'created'];
  protected readonly pageIndex = signal(0);
  protected readonly stats = rxResource({ stream: () => this.api.teamStats() });
  protected readonly tickets = rxResource({
    params: () => this.pageIndex(),
    stream: ({ params }) => this.api.teamTickets(params + 1, 20, null),
  });

  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
  }
}
