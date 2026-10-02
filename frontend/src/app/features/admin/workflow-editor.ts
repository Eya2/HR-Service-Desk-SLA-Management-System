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
import { WorkflowsApi } from '../../core/api/approvals.api';
import { problemOf } from '../../core/http/error.interceptor';
import { humanize } from '../../shared/ui/labels';

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
  ],
  template: `
    <a mat-button routerLink="/admin/workflows"><mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon> Workflows</a>
    @if (workflow.value(); as w) {
      <h1>{{ w.requestTypeName }}</h1>
      @if (w.requestTypeIsConfidential) {
        <p class="note" role="note">Confidential requests can only be approved by HR Admins.</p>
      }
      <mat-card appearance="outlined">
        <mat-card-content>
          <ol class="steps">
            @for (step of steps.controls; track step; let i = $index; let last = $last) {
              <li [formGroup]="step" data-testid="step">
                <span class="number">{{ i + 1 }}</span>
                <mat-form-field appearance="outline" subscriptSizing="dynamic">
                  <mat-label>Step name</mat-label>
                  <input matInput formControlName="name" maxlength="100" />
                </mat-form-field>
                <mat-form-field appearance="outline" subscriptSizing="dynamic">
                  <mat-label>Approver</mat-label>
                  <mat-select formControlName="approverRole">
                    @for (role of roles(); track role) {
                      <mat-option [value]="role">{{ roleLabel(role) }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <button mat-icon-button type="button" (click)="move(i, -1)" [disabled]="i === 0" aria-label="Move up">
                  <mat-icon fontSet="material-symbols-outlined">arrow_upward</mat-icon>
                </button>
                <button mat-icon-button type="button" (click)="move(i, 1)" [disabled]="last" aria-label="Move down">
                  <mat-icon fontSet="material-symbols-outlined">arrow_downward</mat-icon>
                </button>
                <button mat-icon-button type="button" (click)="remove(i)" [disabled]="steps.length === 1" aria-label="Remove step">
                  <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                </button>
              </li>
            }
          </ol>
          <button mat-stroked-button type="button" (click)="add()" [disabled]="steps.length >= maxSteps" data-testid="add-step">
            <mat-icon fontSet="material-symbols-outlined">add</mat-icon> Add a step
          </button>
          <div class="footer">
            <mat-slide-toggle [formControl]="isActive">Workflow active</mat-slide-toggle>
            <button mat-flat-button type="button" (click)="save(w.requestTypeId)" [disabled]="steps.invalid || saving()" data-testid="save-workflow">
              Save
            </button>
          </div>
          <p class="hint">Changes apply to new requests only; requests already submitted keep their approval chain.</p>
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
    .note {
      padding: 8px 12px;
      border-radius: 8px;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    mat-card {
      max-width: 820px;
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
  private readonly router = inject(Router);

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
      const initial = w.steps.length > 0 ? w.steps : [{ name: 'Manager approval', approverRole: this.roles()[0] }];
      initial.forEach((s) => this.steps.push(this.stepForm(s.name, s.approverRole)));
      this.isActive.setValue(w.isConfigured ? w.isActive : true);
    });
  }

  protected roleLabel(role: string): string {
    return role === 'Manager' ? "Requester's manager" : humanize(role);
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
        this.snackBar.open('Workflow saved.', undefined, { duration: 3000 });
        void this.router.navigate(['/admin/workflows']);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        const problem = problemOf(error);
        const detail = problem?.errors ? Object.values(problem.errors).flat().join(' ') : problem?.detail ?? problem?.title;
        this.snackBar.open(detail ?? 'The workflow could not be saved.', 'Dismiss', { duration: 8000 });
      },
    });
  }

  private stepForm(name: string, approverRole: string): StepForm {
    return new FormGroup({
      name: new FormControl(name, { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
      approverRole: new FormControl(approverRole, { nonNullable: true, validators: Validators.required }),
    });
  }
}
