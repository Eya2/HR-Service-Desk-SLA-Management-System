import { DatePipe, formatDate, formatNumber } from '@angular/common';
import { Component, LOCALE_ID, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApprovalInfo, AttachmentInfo, FormAnswer, PRIORITIES, TeamInfo, TicketDetails } from '../../core/api/api.models';
import { Observable } from 'rxjs';
import { ApprovalsApi, TeamsApi } from '../../core/api/approvals.api';
import { TicketsApi } from '../../core/api/tickets.api';
import { problemOf } from '../../core/http/error.interceptor';
import { fileSize, humanize } from '../../shared/ui/labels';
import { SlaBadge } from '../../shared/ui/sla-badge';
import { StatusChip } from '../../shared/ui/status-chip';
import { ReasonDialog, ReasonRequest } from './reason-dialog';
import { StatusAction, describeEvent, statusAction } from './status-actions';

/** A case: answers, documents and the conversation. Actions follow the permissions sent by the API. */
@Component({
  selector: 'app-ticket-detail',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    SlaBadge,
    StatusChip,
  ],
  template: `
    @if (ticket.isLoading() && !ticket.value()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (ticket.error()) {
      <h1>Case not found</h1>
      <p>It does not exist or you are not allowed to see it.</p>
    } @else if (ticket.value(); as t) {
      <header class="header">
        <div>
          <p class="reference">{{ t.reference }} · {{ t.requestTypeName }}</p>
          @if (!editing()) {
            <h1 data-testid="ticket-title">{{ t.title }}</h1>
          }
        </div>
        <div class="chips">
          <app-status-chip [value]="t.status" />
          <app-status-chip [value]="t.priority" />
          @if (t.isConfidential) {
            <span class="confidential"><mat-icon fontSet="material-symbols-outlined">lock</mat-icon>Confidential</span>
          }
        </div>
      </header>

      @if (actions().length > 0) {
        <div class="status-actions" role="group" aria-label="Case actions">
          @for (action of actions(); track action.status) {
            @if (action.primary) {
              <button mat-flat-button type="button" (click)="changeStatus(t, action)" [disabled]="changing()" [attr.data-status]="action.status">
                <mat-icon fontSet="material-symbols-outlined">{{ action.icon }}</mat-icon>{{ action.label }}
              </button>
            } @else {
              <button mat-stroked-button type="button" (click)="changeStatus(t, action)" [disabled]="changing()" [attr.data-status]="action.status">
                <mat-icon fontSet="material-symbols-outlined">{{ action.icon }}</mat-icon>{{ action.label }}
              </button>
            }
          }
        </div>
      }

      <div class="layout">
        <div class="main">
          @if (editing()) {
            <mat-card appearance="outlined">
              <mat-card-content>
                <form [formGroup]="editForm" (ngSubmit)="saveEdit(t)" class="edit">
                  <mat-form-field appearance="outline">
                    <mat-label>Title</mat-label>
                    <input matInput formControlName="title" maxlength="200" />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Additional details</mat-label>
                    <textarea matInput formControlName="description" rows="4" maxlength="4000"></textarea>
                  </mat-form-field>
                  @if (t.permissions.canChangePriority) {
                    <mat-form-field appearance="outline">
                      <mat-label>Priority</mat-label>
                      <mat-select formControlName="priority">
                        @for (p of priorities; track p) {
                          <mat-option [value]="p">{{ p }}</mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                  }
                  <div class="actions">
                    <button mat-button type="button" (click)="editing.set(false)">Cancel</button>
                    <button mat-flat-button type="submit" [disabled]="editForm.invalid">Save</button>
                  </div>
                </form>
              </mat-card-content>
            </mat-card>
          } @else {
            <mat-card appearance="outlined">
              <mat-card-header>
                <mat-card-title>Request</mat-card-title>
                @if (t.permissions.canEdit) {
                  <button mat-icon-button class="edit-button" (click)="startEdit(t)" aria-label="Edit request">
                    <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
                  </button>
                }
              </mat-card-header>
              <mat-card-content>
                @if (t.description) {
                  <p class="description">{{ t.description }}</p>
                }
                <dl class="answers">
                  @for (answer of t.answers; track answer.key) {
                    <dt>{{ answer.label }}</dt>
                    <dd [attr.data-answer]="answer.key">
                      @if (answer.type === 'File') {
                        @for (file of answer.files; track file.id) {
                          <button mat-button type="button" (click)="download(t, file)">
                            <mat-icon fontSet="material-symbols-outlined">download</mat-icon>{{ file.fileName }}
                          </button>
                        }
                      } @else {
                        {{ display(answer) }}
                      }
                    </dd>
                  }
                </dl>
              </mat-card-content>
            </mat-card>
          }

          @if (t.approvals.length > 0) {
            <mat-card appearance="outlined" data-testid="approvals">
              <mat-card-header><mat-card-title>Approvals</mat-card-title></mat-card-header>
              <mat-card-content>
                <ol class="approvals">
                  @for (step of t.approvals; track step.id) {
                    <li [attr.data-decision]="step.decision">
                      <mat-icon fontSet="material-symbols-outlined">{{ decisionIcon(step) }}</mat-icon>
                      <div>
                        <strong>{{ step.stepName }}</strong>
                        <span class="approver">· {{ step.approverName ?? humanize(step.approverRole) }}</span>
                        <div class="decision">
                          {{ humanize(step.decision) }}
                          @if (step.decidedByName) {
                            by {{ step.decidedByName }} · {{ step.decidedAt | date: 'medium' }}
                          }
                        </div>
                        @if (step.comment) {
                          <q>{{ step.comment }}</q>
                        }
                      </div>
                    </li>
                  }
                </ol>
                @if (t.permissions.decidableApprovalId; as approvalId) {
                  <div class="actions">
                    <button mat-stroked-button type="button" (click)="decide(t, approvalId, false)" [disabled]="changing()" data-testid="reject-approval">
                      <mat-icon fontSet="material-symbols-outlined">block</mat-icon>Reject
                    </button>
                    <button mat-flat-button type="button" (click)="decide(t, approvalId, true)" [disabled]="changing()" data-testid="approve">
                      <mat-icon fontSet="material-symbols-outlined">check</mat-icon>Approve
                    </button>
                  </div>
                }
              </mat-card-content>
            </mat-card>
          }

          <mat-card appearance="outlined">
            <mat-card-header><mat-card-title>Conversation</mat-card-title></mat-card-header>
            <mat-card-content>
              <ol class="comments">
                @for (comment of t.comments; track comment.id) {
                  <li [class.internal]="comment.isInternal" data-testid="comment">
                    <div class="meta">
                      <strong>{{ comment.authorName }}</strong>
                      <span>{{ comment.createdAt | date: 'medium' }}</span>
                      @if (comment.isInternal) {
                        <span class="internal-tag">Internal note</span>
                      }
                    </div>
                    <p>{{ comment.body }}</p>
                  </li>
                } @empty {
                  <li class="empty">No messages yet.</li>
                }
              </ol>

              @if (t.permissions.canComment) {
                <form [formGroup]="commentForm" (ngSubmit)="sendComment(t)" class="reply">
                  <mat-form-field appearance="outline">
                    <mat-label>{{ commentForm.controls.isInternal.value ? 'Internal note (HR only)' : 'Reply' }}</mat-label>
                    <textarea matInput formControlName="body" rows="3" maxlength="4000" data-testid="comment-body"></textarea>
                  </mat-form-field>
                  <div class="actions">
                    @if (t.permissions.canCommentInternally) {
                      <mat-checkbox formControlName="isInternal" data-testid="internal-toggle">Internal note</mat-checkbox>
                    }
                    <button mat-flat-button type="submit" [disabled]="commentForm.invalid || sending()">Send</button>
                  </div>
                </form>
              }
            </mat-card-content>
          </mat-card>
        </div>

        <aside class="side">
          <mat-card appearance="outlined">
            <mat-card-content>
              <dl class="facts">
                <dt>Requester</dt>
                <dd>{{ t.requester.fullName }}</dd>
                <dt>Team</dt>
                <dd data-testid="team">{{ t.team?.name ?? '—' }}</dd>
                <dt>Assignee</dt>
                <dd data-testid="assignee">{{ t.assignee?.fullName ?? 'Not assigned yet' }}</dd>
                <dt>Submitted</dt>
                <dd>{{ t.createdAt | date: 'medium' }}</dd>
              </dl>
              @if (t.permissions.canClaim && !t.assignee) {
                <button mat-flat-button type="button" class="take" (click)="claim(t)" [disabled]="changing()" data-testid="take">
                  <mat-icon fontSet="material-symbols-outlined">front_hand</mat-icon> Take this case
                </button>
              }
              @if (t.permissions.canAssign) {
                @if (teams.value(); as teamList) {
                  <div class="assign">
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                      <mat-label>Assign to</mat-label>
                      <mat-select [value]="t.assignee?.id ?? null" (selectionChange)="assign(t, $event.value)" data-testid="assign-select">
                        <mat-option [value]="null">Nobody (team queue)</mat-option>
                        @for (member of membersOf(t, teamList); track member.id) {
                          <mat-option [value]="member.id">{{ member.fullName }} ({{ member.activeCases }} active)</mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                      <mat-label>Team</mat-label>
                      <mat-select [value]="t.team?.id ?? null" (selectionChange)="moveToTeam(t, $event.value)" data-testid="team-select">
                        @for (team of teamList; track team.id) {
                          <mat-option [value]="team.id">{{ team.name }}</mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                  </div>
                }
              }
            </mat-card-content>
          </mat-card>

          @if (t.sla.state !== 'None') {
            <mat-card appearance="outlined" data-testid="sla">
              <mat-card-header><mat-card-title>Service level</mat-card-title></mat-card-header>
              <mat-card-content>
                <app-sla-badge [state]="t.sla.state" [dueAt]="t.sla.resolutionDueAt" [paused]="t.sla.isPaused" />
                <dl class="facts sla">
                  <dt>First response</dt>
                  <dd>
                    @if (t.sla.firstRespondedAt) {
                      {{ t.sla.firstResponseBreached ? 'Late' : 'On time' }} · {{ t.sla.firstRespondedAt | date: 'short' }}
                    } @else if (t.sla.firstResponseDueAt) {
                      due {{ t.sla.firstResponseDueAt | date: 'short' }}
                    } @else {
                      paused
                    }
                  </dd>
                  <dt>Resolution</dt>
                  <dd>
                    @if (t.sla.resolutionDueAt) {
                      due {{ t.sla.resolutionDueAt | date: 'short' }}
                    } @else {
                      paused
                    }
                  </dd>
                  <dt>Targets</dt>
                  <dd>{{ hours(t.sla.firstResponseTargetMinutes) }} / {{ hours(t.sla.resolutionTargetMinutes) }} (business hours)</dd>
                </dl>
              </mat-card-content>
            </mat-card>
          }

          <mat-card appearance="outlined">
            <mat-card-header><mat-card-title>Documents</mat-card-title></mat-card-header>
            <mat-card-content>
              <ul class="documents">
                @for (file of t.attachments; track file.id) {
                  <li>
                    <button mat-button type="button" (click)="download(t, file)" [attr.data-testid]="'doc-' + file.fileName">
                      <mat-icon fontSet="material-symbols-outlined">description</mat-icon>{{ file.fileName }}
                    </button>
                    <span class="size">{{ size(file.sizeBytes) }}</span>
                  </li>
                } @empty {
                  <li class="empty">No documents.</li>
                }
              </ul>
              @if (t.permissions.canAttach) {
                <input #picker type="file" hidden multiple (change)="upload(t, picker.files); picker.value = ''" />
                <button mat-stroked-button type="button" (click)="picker.click()" [disabled]="uploading()">
                  <mat-icon fontSet="material-symbols-outlined">upload</mat-icon> Add documents
                </button>
              }
            </mat-card-content>
          </mat-card>

          <mat-card appearance="outlined">
            <mat-card-header><mat-card-title>History</mat-card-title></mat-card-header>
            <mat-card-content>
              <ol class="timeline">
                @for (entry of t.timeline; track entry.id) {
                  <li data-testid="timeline-entry">
                    <span class="who">{{ entry.actorName ?? 'System' }}</span> {{ describe(entry.type, entry.data) }}
                    @if (entry.data?.['reason']; as reason) {
                      <q>{{ reason }}</q>
                    }
                    <time [attr.datetime]="entry.occurredAt">{{ entry.occurredAt | date: 'medium' }}</time>
                  </li>
                }
              </ol>
            </mat-card-content>
          </mat-card>
        </aside>
      </div>
    }
  `,
  styles: `
    .header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 16px;
      flex-wrap: wrap;
    }
    .reference {
      color: var(--mat-sys-on-surface-variant);
      margin: 0;
    }
    h1 {
      font: var(--mat-sys-headline-small);
      margin: 4px 0 16px;
    }
    .chips {
      display: flex;
      gap: 8px;
      align-items: center;
    }
    .confidential {
      display: inline-flex;
      align-items: center;
      gap: 2px;
      color: var(--mat-sys-error);
      font: var(--mat-sys-label-medium);
    }
    .status-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-bottom: 16px;
    }
    .timeline {
      list-style: none;
      margin: 0;
      padding: 0 0 0 12px;
      border-left: 2px solid var(--mat-sys-outline-variant);
      display: grid;
      gap: 12px;
    }
    .timeline li {
      font: var(--mat-sys-body-small);
    }
    .timeline .who {
      font-weight: 500;
    }
    .timeline q {
      display: block;
      font-style: italic;
      color: var(--mat-sys-on-surface-variant);
    }
    .timeline time {
      display: block;
      color: var(--mat-sys-on-surface-variant);
    }
    .sla {
      margin-top: 12px;
    }
    .take {
      width: 100%;
      margin-top: 16px;
    }
    .assign {
      display: grid;
      gap: 12px;
      margin-top: 16px;
    }
    .approvals {
      list-style: none;
      padding: 0;
      margin: 0 0 12px;
      display: grid;
      gap: 12px;
    }
    .approvals li {
      display: flex;
      gap: 12px;
      align-items: flex-start;
    }
    .approvals li[data-decision='Approved'] mat-icon {
      color: var(--mat-sys-primary);
    }
    .approvals li[data-decision='Rejected'] mat-icon {
      color: var(--mat-sys-error);
    }
    .approvals .approver,
    .approvals .decision {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .approvals q {
      display: block;
      font-style: italic;
    }
    .layout {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 300px;
      gap: 16px;
      align-items: start;
    }
    @media (max-width: 899px) {
      .layout {
        grid-template-columns: 1fr;
      }
    }
    .main,
    .side {
      display: grid;
      gap: 16px;
    }
    mat-card-header {
      align-items: center;
    }
    .edit-button {
      margin-left: auto;
    }
    .description {
      white-space: pre-line;
    }
    .answers,
    .facts {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 8px 16px;
      margin: 0;
    }
    dt {
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
      white-space: pre-line;
    }
    .comments {
      list-style: none;
      padding: 0;
      margin: 0 0 16px;
      display: grid;
      gap: 12px;
    }
    .comments li {
      padding: 12px;
      border-radius: 8px;
      background: var(--mat-sys-surface-container);
    }
    .comments li.internal {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    .comments li.empty {
      background: none;
      color: var(--mat-sys-on-surface-variant);
    }
    .meta {
      display: flex;
      gap: 8px;
      align-items: baseline;
      flex-wrap: wrap;
      font: var(--mat-sys-body-small);
    }
    .internal-tag {
      font-weight: 500;
    }
    .comments p {
      margin: 4px 0 0;
      white-space: pre-line;
    }
    .reply,
    .edit {
      display: grid;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      align-items: center;
      gap: 16px;
    }
    .documents {
      list-style: none;
      padding: 0;
      margin: 0 0 12px;
    }
    .documents li {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .size {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class TicketDetail {
  private readonly api = inject(TicketsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly locale = inject(LOCALE_ID);

  /** Route parameter. */
  readonly id = input.required<string>();

  protected readonly ticket = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.get(params) });
  protected readonly priorities = PRIORITIES;
  protected readonly size = fileSize;
  protected readonly editing = signal(false);
  protected readonly sending = signal(false);
  protected readonly uploading = signal(false);
  protected readonly changing = signal(false);
  protected readonly describe = describeEvent;
  private readonly dialog = inject(MatDialog);
  private readonly approvalsApi = inject(ApprovalsApi);
  private readonly teamsApi = inject(TeamsApi);

  /** Loaded only for people who can assign (the teams endpoint is for HR staff). */
  protected readonly teams = rxResource({
    params: () => (this.ticket.value()?.permissions.canAssign ? true : undefined),
    stream: () => this.teamsApi.list(),
  });
  protected readonly humanize = humanize;

  protected readonly actions = computed<StatusAction[]>(() => {
    const t = this.ticket.value();
    return t ? t.permissions.availableTransitions.map((to) => statusAction(t.status, to)) : [];
  });

  protected readonly commentForm = this.fb.group({
    body: ['', [Validators.required, Validators.maxLength(4000)]],
    isInternal: [false],
  });
  protected readonly editForm = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(4000)],
    priority: [''],
  });

  protected display(answer: FormAnswer): string {
    const value = answer.value;
    if (answer.type === 'Select' && answer.displayValue) return answer.displayValue;
    if (answer.type === 'Number' && typeof value === 'number') return formatNumber(value, this.locale);
    if (answer.type === 'Date' && typeof value === 'string') return formatDate(value, 'mediumDate', this.locale);
    return value === null || value === undefined ? '' : `${value}`;
  }

  protected changeStatus(ticket: TicketDetails, action: StatusAction): void {
    if (action.reason === 'none') {
      this.applyStatus(ticket, action, null);
      return;
    }
    this.dialog
      .open<ReasonDialog, ReasonRequest, string>(ReasonDialog, { data: action })
      .afterClosed()
      .subscribe((reason) => {
        if (reason !== undefined) this.applyStatus(ticket, action, reason || null);
      });
  }

  private applyStatus(ticket: TicketDetails, action: StatusAction, reason: string | null): void {
    this.changing.set(true);
    this.api.changeStatus(ticket.id, action.status, reason).subscribe({
      next: () => {
        this.changing.set(false);
        this.snackBar.open(`${action.label}: done.`, undefined, { duration: 3000 });
        this.ticket.reload();
      },
      error: (error: unknown) => {
        this.changing.set(false);
        this.snackBar.open(problemOf(error)?.title ?? 'The status could not be changed.', 'Dismiss', { duration: 6000 });
        this.ticket.reload();
      },
    });
  }

  /** Members of the case's team first; every other HR staff member after them. */
  protected membersOf(ticket: TicketDetails, teams: TeamInfo[]): TeamInfo['members'] {
    const own = teams.find((t) => t.id === ticket.team?.id)?.members ?? [];
    const others = teams.flatMap((t) => t.members).filter((m) => !own.some((o) => o.id === m.id));
    return [...own, ...others.filter((m, i) => others.findIndex((o) => o.id === m.id) === i)];
  }

  protected claim(ticket: TicketDetails): void {
    this.run(this.api.claim(ticket.id), 'The case is now yours.');
  }

  protected assign(ticket: TicketDetails, assigneeId: string | null): void {
    this.run(this.api.assign(ticket.id, assigneeId), assigneeId ? 'Case assigned.' : 'Case returned to the team queue.');
  }

  protected moveToTeam(ticket: TicketDetails, teamId: string): void {
    this.run(this.api.moveToTeam(ticket.id, teamId), 'Case moved to the other team.');
  }

  private run(action: Observable<void>, success: string): void {
    this.changing.set(true);
    action.subscribe({
      next: () => {
        this.changing.set(false);
        this.snackBar.open(success, undefined, { duration: 3000 });
        this.ticket.reload();
        this.teams.reload();
      },
      error: (error: unknown) => {
        this.changing.set(false);
        this.snackBar.open(problemOf(error)?.title ?? 'The change could not be saved.', 'Dismiss', { duration: 6000 });
        this.ticket.reload();
      },
    });
  }

  protected hours(minutes: number | null): string {
    return minutes === null ? '—' : `${+(minutes / 60).toFixed(1)}h`;
  }

  protected decisionIcon(step: ApprovalInfo): string {
    return { Approved: 'check_circle', Rejected: 'cancel', Skipped: 'remove_circle_outline', Pending: 'hourglass_empty' }[step.decision];
  }

  /** Approving needs no message; rejecting asks for the reason the employee will read. */
  protected decide(ticket: TicketDetails, approvalId: string, approve: boolean): void {
    const send = (comment: string | null) => {
      this.changing.set(true);
      this.approvalsApi.decide(ticket.id, approvalId, approve, comment).subscribe({
        next: () => {
          this.changing.set(false);
          this.snackBar.open(approve ? 'Request approved.' : 'Request rejected.', undefined, { duration: 3000 });
          this.ticket.reload();
        },
        error: (error: unknown) => {
          this.changing.set(false);
          this.snackBar.open(problemOf(error)?.title ?? 'The decision could not be recorded.', 'Dismiss', { duration: 6000 });
          this.ticket.reload();
        },
      });
    };

    if (approve) {
      send(null);
      return;
    }
    this.dialog
      .open<ReasonDialog, ReasonRequest, string>(ReasonDialog, {
        data: { label: 'Reject request', reasonLabel: 'Reason (sent to the employee)', reason: 'required' },
      })
      .afterClosed()
      .subscribe((reason) => {
        if (reason) send(reason);
      });
  }

  protected download(ticket: TicketDetails, file: AttachmentInfo): void {
    this.api.download(ticket.id, file);
  }

  protected startEdit(ticket: TicketDetails): void {
    this.editForm.setValue({ title: ticket.title, description: ticket.description, priority: ticket.priority });
    this.editing.set(true);
  }

  protected saveEdit(ticket: TicketDetails): void {
    const { title, description, priority } = this.editForm.getRawValue();
    this.api
      .update(ticket.id, { title, description, priority: ticket.permissions.canChangePriority ? priority : null })
      .subscribe({
        next: () => {
          this.editing.set(false);
          this.ticket.reload();
        },
        error: (error: unknown) => this.snackBar.open(problemOf(error)?.title ?? 'Could not save.', 'Dismiss', { duration: 6000 }),
      });
  }

  protected sendComment(ticket: TicketDetails): void {
    const { body, isInternal } = this.commentForm.getRawValue();
    this.sending.set(true);
    this.api.addComment(ticket.id, body, isInternal).subscribe({
      next: (comment) => {
        this.sending.set(false);
        this.commentForm.reset({ body: '', isInternal: false });
        this.ticket.update((t) => (t ? { ...t, comments: [...t.comments, comment] } : t));
      },
      error: (error: unknown) => {
        this.sending.set(false);
        this.snackBar.open(problemOf(error)?.title ?? 'Could not send the message.', 'Dismiss', { duration: 6000 });
      },
    });
  }

  protected upload(ticket: TicketDetails, list: FileList | null): void {
    if (!list || list.length === 0) return;
    this.uploading.set(true);
    this.api.addAttachments(ticket.id, Array.from(list)).subscribe({
      next: (added) => {
        this.uploading.set(false);
        this.ticket.update((t) => (t ? { ...t, attachments: [...t.attachments, ...added] } : t));
      },
      error: (error: unknown) => {
        this.uploading.set(false);
        const problem = problemOf(error);
        const detail = problem?.errors ? Object.values(problem.errors).flat().join(' ') : problem?.title;
        this.snackBar.open(detail ?? 'Upload failed.', 'Dismiss', { duration: 8000 });
      },
    });
  }
}
