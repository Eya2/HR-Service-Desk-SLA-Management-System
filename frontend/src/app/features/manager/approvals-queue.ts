import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { PendingApproval } from '../../core/api/api.models';
import { ApprovalsApi } from '../../core/api/approvals.api';
import { problemMessage } from '../../core/http/error.interceptor';
import { ReasonDialog, ReasonRequest } from '../tickets/reason-dialog';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

/** Requests waiting for the user's decision, oldest first, with one-click approval. */
@Component({
  selector: 'app-approvals-queue',
  imports: [DatePipe, MatButtonModule, MatCardModule, MatIconModule, MatProgressBarModule, RouterLink, TranslatePipe],
  template: `
    <h1>{{ 'manager.pending' | translate }}</h1>
    @if (queue.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    <div class="list">
      @for (item of queue.value() ?? []; track item.approvalId) {
        <mat-card appearance="outlined" [attr.data-testid]="'approval-' + item.reference">
          <mat-card-content class="item">
            <div class="info">
              <a [routerLink]="['/tickets', item.ticketId]" class="reference">{{ item.reference }}</a>
              <h2>{{ item.title }}</h2>
              <p>
                {{ 'manager.submittedOn' | translate: { type: item.requestTypeName, name: item.requesterName, date: (item.submittedAt | date: 'medium') } }}
              </p>
              <p class="step">{{ 'manager.step' | translate: { order: item.stepOrder, count: item.stepCount, name: item.stepName } }}</p>
            </div>
            <div class="buttons">
              <button mat-stroked-button type="button" (click)="decide(item, false)" [disabled]="busy() === item.approvalId">
                <mat-icon fontSet="material-symbols-outlined">block</mat-icon>{{ 'ticket.reject' | translate }}
              </button>
              <button mat-flat-button type="button" (click)="decide(item, true)" [disabled]="busy() === item.approvalId" data-testid="approve">
                <mat-icon fontSet="material-symbols-outlined">check</mat-icon>{{ 'ticket.approve' | translate }}
              </button>
            </div>
          </mat-card-content>
        </mat-card>
      } @empty {
        @if (!queue.isLoading()) {
          <p class="empty" data-testid="queue-empty">{{ 'manager.nothing' | translate }}</p>
        }
      }
    </div>
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
    .list {
      display: grid;
      gap: 12px;
    }
    .item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 16px;
      flex-wrap: wrap;
    }
    .reference {
      font: var(--mat-sys-label-large);
    }
    h2 {
      font: var(--mat-sys-title-medium);
      margin: 4px 0;
    }
    p {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .step {
      margin-top: 4px;
      color: var(--mat-sys-on-surface);
    }
    .buttons {
      display: flex;
      gap: 8px;
    }
  `,
})
export class ApprovalsQueue {
  private readonly api = inject(ApprovalsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  protected readonly queue = rxResource({ stream: () => this.api.pending() });
  protected readonly busy = signal<string | null>(null);

  protected decide(item: PendingApproval, approve: boolean): void {
    if (approve) {
      this.send(item, true, null);
      return;
    }
    this.dialog
      .open<ReasonDialog, ReasonRequest, string>(ReasonDialog, {
        data: { label: this.translate.instant('manager.rejectRef', { reference: item.reference }), reasonLabel: 'action.reasonToEmployee', reason: 'required' },
      })
      .afterClosed()
      .subscribe((reason) => {
        if (reason) this.send(item, false, reason);
      });
  }

  private send(item: PendingApproval, approve: boolean, comment: string | null): void {
    this.busy.set(item.approvalId);
    this.api.decide(item.ticketId, item.approvalId, approve, comment).subscribe({
      next: () => {
        this.busy.set(null);
        this.queue.update((items) => items?.filter((i) => i.approvalId !== item.approvalId));
        this.snackBar.open(this.translate.instant(approve ? 'manager.approvedRef' : 'manager.rejectedRef', { reference: item.reference }), undefined, {
          duration: 3000,
        });
      },
      error: (error: unknown) => {
        this.busy.set(null);
        this.snackBar.open(problemMessage(this.translate, error, 'ticket.decisionFailed'), this.translate.instant('common.dismiss'), { duration: 6000 });
        this.queue.reload();
      },
    });
  }
}
