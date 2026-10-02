import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { STATUSES, TicketSummary } from '../../core/api/api.models';
import { TicketScope, TicketsApi } from '../../core/api/tickets.api';
import { AuthService } from '../../core/auth/auth.service';
import { problemMessage } from '../../core/http/error.interceptor';
import { SlaBadge } from '../../shared/ui/sla-badge';
import { StatusChip } from '../../shared/ui/status-chip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { EnumLabelPipe } from '../../shared/ui/enum-label';

/** The HR agent's work queue: my cases, my teams', unassigned ones, or everything. */
@Component({
  selector: 'app-queue',
  imports: [
    DatePipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTableModule,
    RouterLink,
    SlaBadge,
    StatusChip,
    TranslatePipe,
    EnumLabelPipe,
  ],
  template: `
    <h1>{{ 'queue.title' | translate }}</h1>
    <div class="filters">
      <mat-button-toggle-group [value]="scope()" (change)="scope.set($event.value); page.set(0)" [attr.aria-label]="'queue.view' | translate">
        @for (s of scopes(); track s.value) {
          <mat-button-toggle [value]="s.value" [attr.data-testid]="'scope-' + s.value">{{ s.label | translate }}</mat-button-toggle>
        }
      </mat-button-toggle-group>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ 'queue.search' | translate }}</mat-label>
        <input matInput [value]="search()" (input)="search.set($any($event.target).value)" />
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ 'requests.status' | translate }}</mat-label>
        <mat-select [value]="status()" (selectionChange)="status.set($event.value); page.set(0)">
          <mat-option [value]="null">{{ 'queue.any' | translate }}</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ s | enumLabel: 'status' }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-slide-toggle [checked]="activeOnly()" (change)="activeOnly.set($event.checked); page.set(0)">{{ 'queue.activeOnly' | translate }}</mat-slide-toggle>
    </div>

    @if (tickets.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (tickets.value(); as result) {
      <table mat-table [dataSource]="result.items" class="table">
        <ng-container matColumnDef="reference">
          <th mat-header-cell *matHeaderCellDef>{{ 'requests.reference' | translate }}</th>
          <td mat-cell *matCellDef="let t"><a [routerLink]="['/tickets', t.id]">{{ t.reference }}</a></td>
        </ng-container>
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef>{{ 'queue.case' | translate }}</th>
          <td mat-cell *matCellDef="let t">
            <div>{{ t.title }}</div>
            <div class="sub">{{ t.requestTypeName }} · {{ t.requesterName }}</div>
          </td>
        </ng-container>
        <ng-container matColumnDef="assignee">
          <th mat-header-cell *matHeaderCellDef>{{ 'ticket.assignee' | translate }}</th>
          <td mat-cell *matCellDef="let t">
            @if (t.assigneeName) {
              <div>{{ t.assigneeName }}</div>
            } @else if (canWork) {
              <button mat-stroked-button type="button" class="take" (click)="claim(t)" [disabled]="claiming() === t.id" [attr.data-testid]="'take-' + t.reference">
                {{ 'queue.take' | translate }}
              </button>
            } @else {
              <div>—</div>
            }
            <div class="sub">{{ t.teamName ?? ('common.noTeam' | translate) }}</div>
          </td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>{{ 'requests.status' | translate }}</th>
          <td mat-cell *matCellDef="let t"><app-status-chip [value]="t.status" /></td>
        </ng-container>
        <ng-container matColumnDef="priority">
          <th mat-header-cell *matHeaderCellDef>{{ 'ticket.priority' | translate }}</th>
          <td mat-cell *matCellDef="let t"><app-status-chip [value]="t.priority" /></td>
        </ng-container>
        <ng-container matColumnDef="sla">
          <th mat-header-cell *matHeaderCellDef>{{ 'queue.sla' | translate }}</th>
          <td mat-cell *matCellDef="let t"><app-sla-badge [state]="t.slaState" [dueAt]="t.resolutionDueAt" /></td>
        </ng-container>
        <ng-container matColumnDef="created">
          <th mat-header-cell *matHeaderCellDef>{{ 'requests.submitted' | translate }}</th>
          <td mat-cell *matCellDef="let t">{{ t.createdAt | date: 'd MMM' }}</td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns" [attr.data-testid]="'row-' + row.reference"></tr>
      </table>
      @if (result.totalCount === 0) {
        <p class="empty">{{ 'queue.empty' | translate }}</p>
      }
      <mat-paginator
        [length]="result.totalCount"
        [pageIndex]="page()"
        [pageSize]="pageSize()"
        [pageSizeOptions]="[20, 50, 100]"
        (page)="onPage($event)"
      />
    }
  `,
  styles: `
    .mat-column-reference,
    .mat-column-assignee,
    .mat-column-status,
    .mat-column-priority,
    .mat-column-sla,
    .mat-column-created {
      white-space: nowrap;
    }
    .mat-column-title {
      min-width: 220px;
    }
    .take {
      height: 30px;
    }
    .filters {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 16px;
      margin-bottom: 16px;
    }
    .table {
      width: 100%;
    }
    .sub {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class Queue {
  private readonly api = inject(TicketsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);
  private readonly auth = inject(AuthService);

  /** Auditors read the queue but do not take cases. */
  protected readonly canWork = this.auth.hasAnyRole(['HrOfficer', 'PayrollSpecialist', 'HrAdmin']);
  protected readonly statuses = STATUSES;
  protected readonly columns = ['reference', 'title', 'assignee', 'status', 'priority', 'sla', 'created'];
  protected readonly scopes = computed<{ value: TicketScope; label: string }[]>(() =>
    this.canWork
      ? [
          { value: 'Mine', label: 'queue.scope.Mine' },
          { value: 'MyTeams', label: 'queue.scope.MyTeams' },
          { value: 'Unassigned', label: 'queue.scope.Unassigned' },
          { value: 'All', label: 'queue.scope.All' },
        ]
      : [{ value: 'All', label: 'queue.scope.AllCases' }],
  );

  protected readonly scope = signal<TicketScope>(this.canWork ? 'Mine' : 'All');
  protected readonly search = signal('');
  protected readonly status = signal<string | null>(null);
  protected readonly activeOnly = signal(true);
  protected readonly page = signal(0);
  protected readonly pageSize = signal(20);
  protected readonly claiming = signal<string | null>(null);

  private readonly debouncedSearch = toSignal(toObservable(this.search).pipe(debounceTime(300), distinctUntilChanged()), {
    initialValue: '',
  });

  protected readonly tickets = rxResource({
    params: () => ({
      scope: this.scope(),
      search: this.debouncedSearch(),
      status: this.status(),
      activeOnly: this.activeOnly(),
      page: this.page() + 1,
      pageSize: this.pageSize(),
    }),
    stream: ({ params }) => this.api.queue(params),
  });

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.page.set(event.pageIndex);
  }

  protected claim(ticket: TicketSummary): void {
    this.claiming.set(ticket.id);
    this.api.claim(ticket.id).subscribe({
      next: () => {
        this.claiming.set(null);
        this.snackBar.open(this.translate.instant('queue.taken', { reference: ticket.reference }), undefined, { duration: 3000 });
        this.tickets.reload();
      },
      error: (error: unknown) => {
        this.claiming.set(null);
        this.snackBar.open(problemMessage(this.translate, error, 'queue.takeFailed'), this.translate.instant('common.dismiss'), { duration: 6000 });
        this.tickets.reload();
      },
    });
  }
}
