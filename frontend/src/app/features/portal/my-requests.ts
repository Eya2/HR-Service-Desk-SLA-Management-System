import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { STATUSES } from '../../core/api/api.models';
import { TicketsApi } from '../../core/api/tickets.api';
import { humanize } from '../../shared/ui/labels';
import { StatusChip } from '../../shared/ui/status-chip';

@Component({
  selector: 'app-my-requests',
  imports: [
    DatePipe,
    MatFormFieldModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    RouterLink,
    StatusChip,
  ],
  template: `
    <div class="toolbar">
      <h1>My requests</h1>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Status</mat-label>
        <mat-select [value]="status()" (selectionChange)="status.set($event.value); page.set(0)">
          <mat-option [value]="null">All</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ humanize(s) }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>

    @if (tickets.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    @if (tickets.value(); as result) {
      @if (result.totalCount === 0) {
        <p class="empty">You have no requests yet. <a routerLink="/portal/catalog">Browse the catalog</a> to submit one.</p>
      } @else {
        <table mat-table [dataSource]="result.items" class="table">
          <ng-container matColumnDef="reference">
            <th mat-header-cell *matHeaderCellDef>Reference</th>
            <td mat-cell *matCellDef="let t"><a [routerLink]="['/tickets', t.id]">{{ t.reference }}</a></td>
          </ng-container>
          <ng-container matColumnDef="title">
            <th mat-header-cell *matHeaderCellDef>Title</th>
            <td mat-cell *matCellDef="let t">{{ t.title }}</td>
          </ng-container>
          <ng-container matColumnDef="type">
            <th mat-header-cell *matHeaderCellDef>Type</th>
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
          <tr mat-row *matRowDef="let row; columns: columns" [attr.data-testid]="'row-' + row.reference"></tr>
        </table>
        <mat-paginator
          [length]="result.totalCount"
          [pageIndex]="page()"
          [pageSize]="pageSize()"
          [pageSizeOptions]="[10, 20, 50]"
          (page)="onPage($event)"
        />
      }
    }
  `,
  styles: `
    .toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
    }
    h1 {
      font: var(--mat-sys-headline-small);
    }
    .table {
      width: 100%;
    }
  `,
})
export class MyRequests {
  private readonly api = inject(TicketsApi);

  protected readonly statuses = STATUSES;
  protected readonly humanize = humanize;
  protected readonly columns = ['reference', 'title', 'type', 'status', 'created'];
  protected readonly status = signal<string | null>(null);
  protected readonly page = signal(0);
  protected readonly pageSize = signal(20);

  protected readonly tickets = rxResource({
    params: () => ({ status: this.status(), page: this.page() + 1, pageSize: this.pageSize() }),
    stream: ({ params }) => this.api.mine(params),
  });

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.page.set(event.pageIndex);
  }
}
