import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { APPROVER_ROLES } from '../../core/api/api.models';
import { SlaApi, TeamsApi, WorkflowsApi } from '../../core/api/approvals.api';
import { problemMessage, problemOf } from '../../core/http/error.interceptor';
import { enumLabel } from '../../shared/ui/enum-label';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

type StepForm = FormGroup<{ name: FormControl<string>; approverRole: FormControl<string> }>;

const MAX_STEPS = 5;

/** Edits the ordered approval chain of one request type. */
@Component({
  selector: 'app-workflow-editor',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    RouterLink,
    TranslatePipe,
  ],
  template: `
    <a mat-button routerLink="/admin/workflows"><mat-icon class="flip-rtl" fontSet="material-symbols-outlined">arrow_back</mat-icon> {{ 'workflows.back' | translate }}</a>
    @if (workflow.value(); as w) {
      <h1>{{ w.requestTypeName }}</h1>
      @if (w.requestTypeIsConfidential) {
        <p class="note" role="note">{{ 'workflows.confidentialNote' | translate }}</p>
      }
      <mat-card appearance="outlined" class="routing">
        <mat-card-content>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'workflows.handledBy' | translate }}</mat-label>
            <mat-select [value]="w.responsibleTeamId" (selectionChange)="setTeam(w.requestTypeId, $event.value)" data-testid="routing-team">
              <mat-option [value]="null">{{ 'workflows.noTeam' | translate }}</mat-option>
              @for (team of teams.value() ?? []; track team.id) {
                <mat-option [value]="team.id">{{ team.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'workflows.slaPolicy' | translate }}</mat-label>
            <mat-select [value]="policyOf(w.requestTypeName)" (selectionChange)="setPolicy(w.requestTypeId, $event.value)" data-testid="sla-policy">
              @for (policy of policies.value() ?? []; track policy.id) {
                <mat-option [value]="policy.id">{{ policy.name }}{{ policy.isDefault ? ('workflows.default' | translate) : '' }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </mat-card-content>
      </mat-card>

      <h2>{{ 'workflows.chain' | translate }}</h2>
      <mat-card appearance="outlined">
        <mat-card-content>
          <ol class="steps">
            @for (step of steps.controls; track step; let i = $index; let last = $last) {
              <li [formGroup]="step" data-testid="step">
                <span class="number">{{ i + 1 }}</span>
                <mat-form-field appearance="outline" subscriptSizing="dynamic">
                  <mat-label>{{ 'workflows.stepName' | translate }}</mat-label>
                  <input matInput formControlName="name" maxlength="100" />
                </mat-form-field>
                <mat-form-field appearance="outline" subscriptSizing="dynamic">
                  <mat-label>{{ 'workflows.approver' | translate }}</mat-label>
                  <mat-select formControlName="approverRole">
                    @for (role of roles(); track role) {
                      <mat-option [value]="role">{{ roleLabel(role) }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <button mat-icon-button type="button" (click)="move(i, -1)" [disabled]="i === 0" [attr.aria-label]="'workflows.moveUp' | translate">
                  <mat-icon fontSet="material-symbols-outlined">arrow_upward</mat-icon>
                </button>
                <button mat-icon-button type="button" (click)="move(i, 1)" [disabled]="last" [attr.aria-label]="'workflows.moveDown' | translate">
                  <mat-icon fontSet="material-symbols-outlined">arrow_downward</mat-icon>
                </button>
                <button mat-icon-button type="button" (click)="remove(i)" [disabled]="steps.length === 1" [attr.aria-label]="'workflows.removeStep' | translate">
                  <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                </button>
              </li>
            }
          </ol>
          <button mat-stroked-button type="button" (click)="add()" [disabled]="steps.length >= maxSteps" data-testid="add-step">
            <mat-icon fontSet="material-symbols-outlined">add</mat-icon> {{ 'workflows.addStep' | translate }}
          </button>
          <div class="footer">
            <mat-slide-toggle [formControl]="isActive">{{ 'workflows.active' | translate }}</mat-slide-toggle>
            <button mat-flat-button type="button" (click)="save(w.requestTypeId)" [disabled]="steps.invalid || saving()" data-testid="save-workflow">
              {{ 'common.save' | translate }}
            </button>
          </div>
          <p class="hint">{{ 'workflows.hint' | translate }}</p>
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `
    .note {
      padding: 8px 12px;
      border-radius: 8px;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    mat-card {
      max-width: 820px;
    }
    .routing {
      margin-bottom: 16px;
    }
    .routing mat-card-content {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
    }
    h2 {
      font: var(--mat-sys-title-medium);
    }
    .steps {
      list-style: none;
      padding: 0;
      margin: 0 0 16px;
      display: grid;
      gap: 12px;
    }
    .steps li {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
    }
    .number {
      width: 28px;
      height: 28px;
      border-radius: 50%;
      display: grid;
      place-items: center;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
      font: var(--mat-sys-label-large);
    }
    .footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-top: 24px;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class WorkflowEditor {
  private readonly api = inject(WorkflowsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly teamsApi = inject(TeamsApi);

  protected readonly teams = rxResource({ stream: () => this.teamsApi.list() });
  private readonly slaApi = inject(SlaApi);
  protected readonly policies = rxResource({ stream: () => this.slaApi.policies() });

  protected policyOf(requestTypeName: string): string | null {
    return this.policies.value()?.find((p) => p.requestTypes.includes(requestTypeName))?.id ?? null;
  }

  protected setPolicy(requestTypeId: string, policyId: string): void {
    const policy = this.policies.value()?.find((p) => p.id === policyId);
    // Choosing the default policy is stored as "no specific policy", so it follows future default changes.
    this.slaApi.setRequestTypePolicy(requestTypeId, policy?.isDefault ? null : policyId).subscribe({
      next: () => {
        this.snackBar.open(this.translate.instant('workflows.policySaved'), undefined, { duration: 3000 });
        this.policies.reload();
      },
      error: (error: unknown) => this.fail(error, 'ticket.saveFailed'),
    });
  }

  /** Route parameter. */
  readonly requestTypeId = input.required<string>();

  protected readonly maxSteps = MAX_STEPS;
  protected readonly workflow = rxResource({ params: () => this.requestTypeId(), stream: ({ params }) => this.api.get(params) });
  protected readonly steps = new FormArray<StepForm>([]);
  protected readonly isActive = new FormControl(true, { nonNullable: true });
  protected readonly saving = signal(false);

  /** Confidential types may only be approved by HR Admins (mirrors the API rule). */
  protected readonly roles = computed<readonly string[]>(() =>
    this.workflow.value()?.requestTypeIsConfidential ? ['HrAdmin'] : APPROVER_ROLES,
  );

  constructor() {
    effect(() => {
      const w = this.workflow.value();
      if (!w) return;
      this.steps.clear();
      const initial = w.steps.length > 0 ? w.steps : [{ name: this.translate.instant('workflows.defaultStep') as string, approverRole: this.roles()[0] }];
      initial.forEach((s) => this.steps.push(this.stepForm(s.name, s.approverRole)));
      this.isActive.setValue(w.isConfigured ? w.isActive : true);
    });
  }

  protected setTeam(requestTypeId: string, teamId: string | null): void {
    this.teamsApi.setResponsibleTeam(requestTypeId, teamId).subscribe({
      next: () => this.snackBar.open(this.translate.instant('workflows.routingSaved'), undefined, { duration: 3000 }),
      error: (error: unknown) => this.fail(error, 'workflows.routingFailed'),
    });
  }

  protected roleLabel(role: string): string {
    return role === 'Manager' ? this.translate.instant('workflows.requesterManager') : enumLabel(this.translate, 'role', role);
  }

  protected add(): void {
    this.steps.push(this.stepForm('', this.roles()[0]));
  }

  protected remove(index: number): void {
    this.steps.removeAt(index);
  }

  protected move(index: number, offset: number): void {
    const step = this.steps.at(index);
    this.steps.removeAt(index);
    this.steps.insert(index + offset, step);
  }

  protected save(requestTypeId: string): void {
    this.saving.set(true);
    this.api.save(requestTypeId, this.isActive.value, this.steps.getRawValue()).subscribe({
      next: () => {
        this.saving.set(false);
        this.snackBar.open(this.translate.instant('workflows.saved'), undefined, { duration: 3000 });
        void this.router.navigate(['/admin/workflows']);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        const problem = problemOf(error);
        const detail = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : (problem?.detail ?? problemMessage(this.translate, error, 'workflows.saveFailed'));
        this.snackBar.open(detail, this.translate.instant('common.dismiss'), { duration: 8000 });
      },
    });
  }

  private fail(error: unknown, fallbackKey: string): void {
    this.snackBar.open(problemMessage(this.translate, error, fallbackKey), this.translate.instant('common.dismiss'), { duration: 6000 });
  }

  private stepForm(name: string, approverRole: string): StepForm {
    return new FormGroup({
      name: new FormControl(name, { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
      approverRole: new FormControl(approverRole, { nonNullable: true, validators: Validators.required }),
    });
  }
}
