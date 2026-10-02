import { DatePipe, formatDate, formatNumber } from '@angular/common';
import { Component, LOCALE_ID, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { problemMessage, problemOf } from '../../core/http/error.interceptor';
import { fileSize } from '../../shared/ui/labels';
import { SlaBadge } from '../../shared/ui/sla-badge';
import { StatusChip } from '../../shared/ui/status-chip';
import { ReasonDialog, ReasonRequest } from './reason-dialog';
import { StatusAction, describeEvent, statusAction } from './status-actions';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { EnumLabelPipe } from '../../shared/ui/enum-label';

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
    TranslatePipe,
    EnumLabelPipe,
  ],
  template: `
    @if (ticket.isLoading() && !ticket.value()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (ticket.error()) {
      <h1>{{ 'ticket.notFound' | translate }}</h1>
      <p>{{ 'ticket.notFoundText' | translate }}</p>
    } @else if (ticket.value(); as t) {
      <header class="header">
        <div>
          <p class="reference">{{ t.reference }} · {{ t.requestTypeName }}</p>
          @if (!editing()) {
            <h1 data-testid="ticket-title" dir="auto">{{ t.title }}</h1>
          }
        </div>
        <div class="chips">
          <app-status-chip [value]="t.status" />
          <app-status-chip [value]="t.priority" />
          @if (t.isConfidential) {
            <span class="confidential"><mat-icon fontSet="material-symbols-outlined">lock</mat-icon>{{ 'ticket.confidential' | translate }}</span>
          }
        </div>
      </header>

      @if (actions().length > 0) {
        <div class="status-actions" role="group" [attr.aria-label]="'ticket.actions' | translate">
          @for (action of actions(); track action.status) {
            @if (action.primary) {
              <button mat-flat-button type="button" (click)="changeStatus(t, action)" [disabled]="changing()" [attr.data-status]="action.status">
                <mat-icon fontSet="material-symbols-outlined">{{ action.icon }}</mat-icon>{{ action.label | translate }}
              </button>
            } @else {
              <button mat-stroked-button type="button" (click)="changeStatus(t, action)" [disabled]="changing()" [attr.data-status]="action.status">
                <mat-icon fontSet="material-symbols-outlined">{{ action.icon }}</mat-icon>{{ action.label | translate }}
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
                    <mat-label>{{ 'ticket.title' | translate }}</mat-label>
                    <input matInput formControlName="title" maxlength="200" />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>{{ 'ticket.details' | translate }}</mat-label>
                    <textarea matInput formControlName="description" rows="4" maxlength="4000"></textarea>
                  </mat-form-field>
                  @if (t.permissions.canChangePriority) {
                    <mat-form-field appearance="outline">
                      <mat-label>{{ 'ticket.priority' | translate }}</mat-label>
                      <mat-select formControlName="priority">
                        @for (p of priorities; track p) {
                          <mat-option [value]="p">{{ p | enumLabel: 'priority' }}</mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                  }
                  <div class="actions">
                    <button mat-button type="button" (click)="editing.set(false)">{{ 'common.cancel' | translate }}</button>
                    <button mat-flat-button type="submit" [disabled]="editForm.invalid">{{ 'common.save' | translate }}</button>
                  </div>
                </form>
              </mat-card-content>
            </mat-card>
          } @else {
            <mat-card appearance="outlined">
              <mat-card-header>
                <mat-card-title>{{ 'ticket.request' | translate }}</mat-card-title>
                @if (t.permissions.canEdit) {
                  <button mat-icon-button class="edit-button" (click)="startEdit(t)" [attr.aria-label]="'ticket.editRequest' | translate">
                    <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
                  </button>
                }
              </mat-card-header>
              <mat-card-content>
                @if (t.description) {
                  <p class="description" dir="auto">{{ t.description }}</p>
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
              <mat-card-header><mat-card-title>{{ 'ticket.approvals' | translate }}</mat-card-title></mat-card-header>
              <mat-card-content>
                <ol class="approvals">
                  @for (step of t.approvals; track step.id) {
                    <li [attr.data-decision]="step.decision">
                      <mat-icon fontSet="material-symbols-outlined">{{ decisionIcon(step) }}</mat-icon>
                      <div>
                        <strong>{{ step.stepName }}</strong>
                        <span class="approver">· {{ step.approverName ?? (step.approverRole | enumLabel: 'role') }}</span>
                        <div class="decision">
                          {{ step.decision | enumLabel: 'decision' }}
                          @if (step.decidedByName) {
                            {{ 'ticket.decidedBy' | translate: { name: step.decidedByName, date: (step.decidedAt | date: 'medium') } }}
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
                      <mat-icon fontSet="material-symbols-outlined">block</mat-icon>{{ 'ticket.reject' | translate }}
                    </button>
                    <button mat-flat-button type="button" (click)="decide(t, approvalId, true)" [disabled]="changing()" data-testid="approve">
                      <mat-icon fontSet="material-symbols-outlined">check</mat-icon>{{ 'ticket.approve' | translate }}
                    </button>
                  </div>
                }
              </mat-card-content>
            </mat-card>
          }

          <mat-card appearance="outlined">
            <mat-card-header><mat-card-title>{{ 'ticket.conversation' | translate }}</mat-card-title></mat-card-header>
            <mat-card-content>
              <ol class="comments">
                @for (comment of t.comments; track comment.id) {
                  <li [class.internal]="comment.isInternal" data-testid="comment">
                    <div class="meta">
                      <strong>{{ comment.authorName }}</strong>
                      <span>{{ comment.createdAt | date: 'medium' }}</span>
                      @if (comment.isInternal) {
                        <span class="internal-tag">{{ 'ticket.internalNote' | translate }}</span>
                      }
                    </div>
                    <p dir="auto">{{ comment.body }}</p>
                  </li>
                } @empty {
                  <li class="empty">{{ 'ticket.noMessages' | translate }}</li>
                }
              </ol>

              @if (t.permissions.canComment) {
                <form [formGroup]="commentForm" (ngSubmit)="sendComment(t)" class="reply">
                  <mat-form-field appearance="outline">
                    <mat-label>{{ (commentForm.controls.isInternal.value ? 'ticket.internalNoteLabel' : 'ticket.reply') | translate }}</mat-label>
                    <textarea matInput formControlName="body" rows="3" maxlength="4000" data-testid="comment-body"></textarea>
                  </mat-form-field>
                  <div class="actions">
                    @if (t.permissions.canCommentInternally) {
                      <mat-checkbox formControlName="isInternal" data-testid="internal-toggle">{{ 'ticket.internalNote' | translate }}</mat-checkbox>
                    }
                    <button mat-flat-button type="submit" [disabled]="commentForm.invalid || sending()">{{ 'ticket.send' | translate }}</button>
                  </div>
                </form>
              }
            </mat-card-content>
          </mat-card>
          @if (t.permissions.canRate) {
            <section class="csat" data-testid="csat-form">
              <div class="csat-head">
                <span class="csat-icon"><mat-icon fontSet="material-symbols-outlined">sentiment_satisfied</mat-icon></span>
                <div>
                  <h2>{{ 'csat.title' | translate }}</h2>
                  <p>{{ 'csat.lead' | translate }}</p>
                </div>
              </div>
              <div class="stars" role="radiogroup" [attr.aria-label]="'csat.scale' | translate">
                @for (n of [1, 2, 3, 4, 5]; track n) {
                  <button
                    type="button"
                    class="star"
                    role="radio"
                    [attr.aria-checked]="score() === n"
                    [class.on]="n <= (hovered() || score())"
                    (mouseenter)="hovered.set(n)"
                    (mouseleave)="hovered.set(0)"
                    (click)="score.set(n)"
                    [attr.aria-label]="'csat.star' | translate: { score: n }"
                    [attr.data-testid]="'star-' + n"
                  >
                    <mat-icon fontSet="material-symbols-outlined">star</mat-icon>
                  </button>
                }
                @if (hovered() || score(); as shown) {
                  <span class="star-label">{{ 'csat.label.' + shown | translate }}</span>
                }
              </div>
              <mat-form-field appearance="outline" class="csat-comment" subscriptSizing="dynamic">
                <mat-label>{{ 'csat.comment' | translate }}</mat-label>
                <textarea matInput rows="2" maxlength="1000" [formControl]="ratingComment" data-testid="csat-comment"></textarea>
              </mat-form-field>
              <div class="actions">
                <button mat-flat-button type="button" [disabled]="score() === 0 || rating()" (click)="rate(t)" data-testid="csat-send">
                  {{ 'csat.send' | translate }}
                </button>
              </div>
            </section>
          } @else if (t.satisfaction; as s) {
            <section class="csat given" data-testid="csat-given">
              <span class="csat-label">{{ 'csat.given' | translate }}</span>
              <div class="stars" [attr.aria-label]="'csat.star' | translate: { score: s.score }" role="img">
                @for (n of [1, 2, 3, 4, 5]; track n) {
                  <mat-icon fontSet="material-symbols-outlined" class="star-static" [class.on]="n <= s.score" aria-hidden="true">star</mat-icon>
                }
                <span class="star-label">{{ 'csat.label.' + s.score | translate }}</span>
              </div>
              @if (s.comment) {
                <q>{{ s.comment }}</q>
              }
            </section>
          }
        </div>

        <aside class="side">
          <mat-card appearance="outlined">
            <mat-card-content>
              <dl class="facts">
                <dt>{{ 'ticket.requester' | translate }}</dt>
                <dd>{{ t.requester.fullName }}</dd>
                <dt>{{ 'ticket.team' | translate }}</dt>
                <dd data-testid="team">{{ t.team?.name ?? '—' }}</dd>
                <dt>{{ 'ticket.assignee' | translate }}</dt>
                <dd data-testid="assignee">{{ t.assignee?.fullName ?? ('ticket.notAssigned' | translate) }}</dd>
                <dt>{{ 'ticket.submitted' | translate }}</dt>
                <dd>{{ t.createdAt | date: 'medium' }}</dd>
              </dl>
              @if (t.permissions.canClaim && !t.assignee) {
                <button mat-flat-button type="button" class="take" (click)="claim(t)" [disabled]="changing()" data-testid="take">
                  <mat-icon fontSet="material-symbols-outlined">front_hand</mat-icon> {{ 'ticket.take' | translate }}
                </button>
              }
              @if (t.permissions.canAssign) {
                @if (teams.value(); as teamList) {
                  <div class="assign">
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                      <mat-label>{{ 'ticket.assignTo' | translate }}</mat-label>
                      <mat-select [value]="t.assignee?.id ?? null" (selectionChange)="assign(t, $event.value)" data-testid="assign-select">
                        <mat-option [value]="null">{{ 'ticket.nobody' | translate }}</mat-option>
                        @for (member of membersOf(t, teamList); track member.id) {
                          <mat-option [value]="member.id">{{ 'ticket.memberLoad' | translate: { name: member.fullName, count: member.activeCases } }}</mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                      <mat-label>{{ 'ticket.team' | translate }}</mat-label>
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
              <mat-card-header><mat-card-title>{{ 'ticket.serviceLevel' | translate }}</mat-card-title></mat-card-header>
              <mat-card-content>
                <app-sla-badge [state]="t.sla.state" [dueAt]="t.sla.resolutionDueAt" [paused]="t.sla.isPaused" />
                <dl class="facts sla">
                  <dt>{{ 'ticket.firstResponse' | translate }}</dt>
                  <dd>
                    @if (t.sla.firstRespondedAt) {
                      {{ (t.sla.firstResponseBreached ? 'ticket.late' : 'ticket.onTime') | translate }} · {{ t.sla.firstRespondedAt | date: 'short' }}
                    } @else if (t.sla.firstResponseDueAt) {
                      {{ 'ticket.due' | translate: { date: (t.sla.firstResponseDueAt | date: 'short') } }}
                    } @else {
                      {{ 'ticket.paused' | translate }}
                    }
                  </dd>
                  <dt>{{ 'ticket.resolution' | translate }}</dt>
                  <dd>
                    @if (t.sla.resolutionDueAt) {
                      {{ 'ticket.due' | translate: { date: (t.sla.resolutionDueAt | date: 'short') } }}
                    } @else {
                      {{ 'ticket.paused' | translate }}
                    }
                  </dd>
                  <dt>{{ 'ticket.targets' | translate }}</dt>
                  <dd>{{ 'ticket.targetsValue' | translate: { first: hours(t.sla.firstResponseTargetMinutes), resolution: hours(t.sla.resolutionTargetMinutes) } }}</dd>
                </dl>
              </mat-card-content>
            </mat-card>
          }

          <mat-card appearance="outlined">
            <mat-card-header><mat-card-title>{{ 'ticket.documents' | translate }}</mat-card-title></mat-card-header>
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
                  <li class="empty">{{ 'ticket.noDocuments' | translate }}</li>
                }
              </ul>
              @if (t.permissions.canAttach) {
                <input #picker type="file" hidden multiple (change)="upload(t, picker.files); picker.value = ''" />
                <button mat-stroked-button type="button" (click)="picker.click()" [disabled]="uploading()">
                  <mat-icon fontSet="material-symbols-outlined">upload</mat-icon> {{ 'ticket.addDocuments' | translate }}
                </button>
              }
            </mat-card-content>
          </mat-card>

          <mat-card appearance="outlined">
            <mat-card-header><mat-card-title>{{ 'ticket.history' | translate }}</mat-card-title></mat-card-header>
            <mat-card-content>
              <ol class="timeline">
                @for (entry of t.timeline; track entry.id) {
                  <li data-testid="timeline-entry">
                    <span class="who">{{ entry.actorName ?? ('event.system' | translate) }}</span> {{ describe(entry.type, entry.data) }}
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
    .csat {
      display: grid;
      gap: 12px;
      padding: 20px 22px;
      border-radius: 16px;
      border: 1px solid color-mix(in srgb, var(--mat-sys-primary) 30%, transparent);
      background:
        radial-gradient(circle at 100% 0%, color-mix(in srgb, var(--mat-sys-tertiary) 14%, transparent), transparent 60%),
        color-mix(in srgb, var(--mat-sys-primary) 6%, var(--app-card-bg));
    }
    .csat-head {
      display: flex;
      gap: 14px;
      align-items: flex-start;
    }
    .csat-head h2 {
      font: 600 1.1rem/1.3 Inter, 'IBM Plex Sans Arabic', sans-serif;
      margin: 0 0 4px;
    }
    .csat-head p {
      margin: 0;
      color: var(--app-muted);
    }
    .csat-icon {
      flex: none;
      width: 40px;
      height: 40px;
      border-radius: 12px;
      display: grid;
      place-items: center;
      color: #fff;
      background: var(--app-brand-gradient);
    }
    .stars {
      display: flex;
      align-items: center;
      gap: 2px;
    }
    .star {
      border: 0;
      background: none;
      padding: 4px;
      cursor: pointer;
      border-radius: 8px;
      color: var(--app-border);
      line-height: 0;
      transition: transform 100ms, color 100ms;
    }
    .star:hover {
      transform: scale(1.12);
    }
    .star:focus-visible {
      outline: 2px solid var(--mat-sys-primary);
    }
    .star mat-icon,
    .star-static {
      font-size: 32px;
      width: 32px;
      height: 32px;
      font-variation-settings: 'FILL' 1;
    }
    .star-static {
      font-size: 22px;
      width: 22px;
      height: 22px;
      color: var(--app-border);
    }
    .star.on,
    .star-static.on {
      color: #f5a524;
    }
    .star-label {
      margin-inline-start: 10px;
      font-weight: 600;
      color: var(--app-muted);
    }
    .csat-comment {
      width: 100%;
    }
    .csat.given {
      gap: 6px;
    }
    .csat-label {
      font-size: 0.75rem;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--app-muted);
    }
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
  private readonly translate = inject(TranslateService);

  /** Route parameter. */
  readonly id = input.required<string>();

  protected readonly ticket = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.get(params) });
  protected readonly priorities = PRIORITIES;
  protected readonly size = fileSize;
  protected readonly editing = signal(false);
  protected readonly sending = signal(false);
  protected readonly uploading = signal(false);
  protected readonly changing = signal(false);
  protected readonly describe = (type: string, data: Record<string, unknown> | null) => describeEvent(this.translate, type, data);
  private readonly dialog = inject(MatDialog);
  private readonly approvalsApi = inject(ApprovalsApi);
  private readonly teamsApi = inject(TeamsApi);

  /** Loaded only for people who can assign (the teams endpoint is for HR staff). */
  protected readonly teams = rxResource({
    params: () => (this.ticket.value()?.permissions.canAssign ? true : undefined),
    stream: () => this.teamsApi.list(),
  });

  protected readonly actions = computed<StatusAction[]>(() => {
    const t = this.ticket.value();
    return t ? t.permissions.availableTransitions.map((to) => statusAction(t.status, to)) : [];
  });

  /** Satisfaction rating (requester, closed case). */
  protected readonly score = signal(0);
  protected readonly hovered = signal(0);
  protected readonly rating = signal(false);
  protected readonly ratingComment = new FormControl('', { nonNullable: true, validators: Validators.maxLength(1000) });

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
        this.snackBar.open(this.t('action.done', { action: this.t(action.label) }), undefined, { duration: 3000 });
        this.ticket.reload();
      },
      error: (error: unknown) => {
        this.changing.set(false);
        this.fail(error, 'ticket.statusFailed');
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
    this.run(this.api.claim(ticket.id), 'ticket.claimed');
  }

  protected assign(ticket: TicketDetails, assigneeId: string | null): void {
    this.run(this.api.assign(ticket.id, assigneeId), assigneeId ? 'ticket.assigned' : 'ticket.returned');
  }

  protected moveToTeam(ticket: TicketDetails, teamId: string): void {
    this.run(this.api.moveToTeam(ticket.id, teamId), 'ticket.moved');
  }

  private run(action: Observable<void>, successKey: string): void {
    this.changing.set(true);
    action.subscribe({
      next: () => {
        this.changing.set(false);
        this.snackBar.open(this.t(successKey), undefined, { duration: 3000 });
        this.ticket.reload();
        this.teams.reload();
      },
      error: (error: unknown) => {
        this.changing.set(false);
        this.fail(error, 'ticket.changeFailed');
        this.ticket.reload();
      },
    });
  }

  protected hours(minutes: number | null): string {
    return minutes === null ? '—' : this.t('ticket.hoursShort', { hours: formatNumber(minutes / 60, this.locale, '1.0-1') });
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
          this.snackBar.open(this.t(approve ? 'ticket.approved' : 'ticket.rejected'), undefined, { duration: 3000 });
          this.ticket.reload();
        },
        error: (error: unknown) => {
          this.changing.set(false);
          this.fail(error, 'ticket.decisionFailed');
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
        data: { label: 'ticket.rejectRequest', reasonLabel: 'action.reasonToEmployee', reason: 'required' },
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
        error: (error: unknown) => this.fail(error, 'ticket.saveFailed'),
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
        this.fail(error, 'ticket.sendFailed');
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
        const detail = problem?.errors ? Object.values(problem.errors).flat().join(' ') : problemMessage(this.translate, error, 'ticket.uploadFailed');
        this.snackBar.open(detail, this.t('common.dismiss'), { duration: 8000 });
      },
    });
  }

  protected rate(ticket: TicketDetails): void {
    this.rating.set(true);
    const comment = this.ratingComment.value.trim() || null;
    this.api.rate(ticket.id, this.score(), comment).subscribe({
      next: () => {
        this.rating.set(false);
        this.snackBar.open(this.t('csat.thanks'), undefined, { duration: 4000 });
        this.ticket.reload();
      },
      error: (error: unknown) => {
        this.rating.set(false);
        this.fail(error, 'errors.generic');
      },
    });
  }

  private t(key: string, params?: Record<string, unknown>): string {
    return this.translate.instant(key, params) as string;
  }

  private fail(error: unknown, fallbackKey: string): void {
    this.snackBar.open(problemMessage(this.translate, error, fallbackKey), this.t('common.dismiss'), { duration: 6000 });
  }
}
