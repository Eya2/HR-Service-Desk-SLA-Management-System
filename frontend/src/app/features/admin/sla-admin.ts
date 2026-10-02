import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable } from 'rxjs';
import { SlaPolicyInfo } from '../../core/api/api.models';
import { SlaApi } from '../../core/api/approvals.api';
import { problemOf } from '../../core/http/error.interceptor';
import { humanize } from '../../shared/ui/labels';

type TargetForm = FormGroup<{ priority: FormControl<string>; firstResponseHours: FormControl<number>; resolutionHours: FormControl<number> }>;

/** HR Admin: business calendar (holidays, deadline check) and SLA policies. */
@Component({
  selector: 'app-sla-admin',
  imports: [DatePipe, ReactiveFormsModule, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    @if (calendar.value(); as cal) {
      <h1>Business calendar · {{ cal.name }}</h1>
      <p class="intro">Time zone {{ cal.timeZoneId }} · SLA deadlines count only these working hours.</p>
      <div class="columns">
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Working hours</mat-card-title></mat-card-header>
          <mat-card-content>
            <ul class="plain">
              @for (i of cal.workingHours; track $index) {
                <li>{{ humanize(i.day) }} {{ i.start }}–{{ i.end }}</li>
              }
            </ul>
          </mat-card-content>
        </mat-card>

        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Public holidays</mat-card-title></mat-card-header>
          <mat-card-content>
            <form [formGroup]="holidayForm" (ngSubmit)="addHoliday()" class="inline">
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>Date</mat-label>
                <input matInput type="date" formControlName="date" />
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>Name</mat-label>
                <input matInput formControlName="name" maxlength="100" />
              </mat-form-field>
              <button mat-stroked-button type="submit" [disabled]="holidayForm.invalid" data-testid="add-holiday">Add</button>
            </form>
            <ul class="holidays">
              @for (h of cal.holidays; track h.date) {
                <li [attr.data-testid]="'holiday-' + h.date">
                  <span>{{ h.date | date: 'EEE d MMM y' }} · {{ h.name }}</span>
                  <button mat-icon-button type="button" (click)="removeHoliday(h.date)" [attr.aria-label]="'Remove ' + h.name">
                    <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                  </button>
                </li>
              }
            </ul>
          </mat-card-content>
        </mat-card>

        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Check a deadline</mat-card-title></mat-card-header>
          <mat-card-content>
            <form [formGroup]="previewForm" (ngSubmit)="preview()" class="inline">
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>From</mat-label>
                <input matInput type="datetime-local" formControlName="start" />
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>Business hours</mat-label>
                <input matInput type="number" min="0" formControlName="hours" />
              </mat-form-field>
              <button mat-stroked-button type="submit" [disabled]="previewForm.invalid">Compute</button>
            </form>
            @if (previewResult(); as due) {
              <p data-testid="preview-result">Due {{ due | date: 'EEEE d MMMM y, HH:mm' }}</p>
            }
          </mat-card-content>
        </mat-card>
      </div>
    }

    <h1>SLA policies</h1>
    <div class="columns">
      @for (policy of policies.value() ?? []; track policy.id) {
        <mat-card appearance="outlined" [attr.data-testid]="'policy-' + policy.name">
          <mat-card-header>
            <mat-card-title>{{ policy.name }} @if (policy.isDefault) { <span class="default">default</span> }</mat-card-title>
            <mat-card-subtitle>At risk at {{ policy.atRiskThresholdPercent }}% · pauses: {{ pauseLabel(policy) }}</mat-card-subtitle>
            <button mat-icon-button class="edit" (click)="edit(policy)" [attr.aria-label]="'Edit ' + policy.name">
              <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
            </button>
          </mat-card-header>
          <mat-card-content>
            @if (editing() === policy.id) {
              <form [formGroup]="policyForm" (ngSubmit)="save(policy)" class="policy-form">
                @for (target of targets.controls; track $index) {
                  <div [formGroup]="target" class="target">
                    <span class="priority">{{ target.controls.priority.value }}</span>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                      <mat-label>First response (h)</mat-label>
                      <input matInput type="number" min="0.25" step="0.25" formControlName="firstResponseHours" />
                    </mat-form-field>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                      <mat-label>Resolution (h)</mat-label>
                      <input matInput type="number" min="0.25" step="0.25" formControlName="resolutionHours" />
                    </mat-form-field>
                  </div>
                }
                <mat-form-field appearance="outline" subscriptSizing="dynamic">
                  <mat-label>At risk threshold (%)</mat-label>
                  <input matInput type="number" min="50" max="99" formControlName="threshold" />
                </mat-form-field>
                <mat-checkbox formControlName="pauseOnApproval">Pause while pending approval</mat-checkbox>
                <mat-checkbox formControlName="pauseOnEmployee">Pause while waiting on the employee</mat-checkbox>
                <div class="actions">
                  <button mat-button type="button" (click)="editing.set(null)">Cancel</button>
                  <button mat-flat-button type="submit" [disabled]="policyForm.invalid" data-testid="save-policy">Save</button>
                </div>
              </form>
            } @else {
              <table class="targets">
                <tr><th>Priority</th><th>First response</th><th>Resolution</th></tr>
                @for (t of policy.targets; track t.priority) {
                  <tr><td>{{ t.priority }}</td><td>{{ t.firstResponseMinutes / 60 }}h</td><td>{{ t.resolutionMinutes / 60 }}h</td></tr>
                }
              </table>
              @if (policy.requestTypes.length > 0) {
                <p class="types">Applies to: {{ policy.requestTypes.join(', ') }}</p>
              }
            }
          </mat-card-content>
        </mat-card>
      }
    </div>
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
    .intro,
    .types {
      color: var(--mat-sys-on-surface-variant);
    }
    .columns {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 16px;
      margin-bottom: 24px;
      align-items: start;
    }
    .plain,
    .holidays {
      list-style: none;
      padding: 0;
      margin: 0;
    }
    .holidays {
      max-height: 320px;
      overflow: auto;
      margin-top: 12px;
    }
    .holidays li {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .inline {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      align-items: center;
    }
    .edit {
      margin-left: auto;
    }
    .default {
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-primary);
      margin-left: 8px;
    }
    .targets {
      width: 100%;
      border-collapse: collapse;
    }
    .targets th,
    .targets td {
      text-align: left;
      padding: 4px 0;
    }
    .policy-form {
      display: grid;
      gap: 8px;
    }
    .target {
      display: grid;
      grid-template-columns: 80px 1fr 1fr;
      gap: 8px;
      align-items: center;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
  `,
})
export class SlaAdmin {
  private readonly api = inject(SlaApi);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly humanize = humanize;
  protected readonly calendar = rxResource({ stream: () => this.api.calendar() });
  protected readonly policies = rxResource({ stream: () => this.api.policies() });
  protected readonly previewResult = signal<string | null>(null);
  protected readonly editing = signal<string | null>(null);

  protected readonly holidayForm = new FormGroup({
    date: new FormControl('', { nonNullable: true, validators: Validators.required }),
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
  });
  protected readonly previewForm = new FormGroup({
    start: new FormControl('', { nonNullable: true, validators: Validators.required }),
    hours: new FormControl(8, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
  });
  protected readonly targets = new FormArray<TargetForm>([]);
  protected readonly policyForm = new FormGroup({
    targets: this.targets,
    threshold: new FormControl(80, { nonNullable: true, validators: [Validators.required, Validators.min(50), Validators.max(99)] }),
    pauseOnApproval: new FormControl(true, { nonNullable: true }),
    pauseOnEmployee: new FormControl(true, { nonNullable: true }),
  });

  protected pauseLabel(policy: SlaPolicyInfo): string {
    return policy.pauseStatuses.length === 0 ? 'none' : policy.pauseStatuses.map((s) => humanize(s).toLowerCase()).join(', ');
  }

  protected addHoliday(): void {
    const { date, name } = this.holidayForm.getRawValue();
    this.updateCalendar(this.api.addHoliday(date, name), () => this.holidayForm.reset());
  }

  protected removeHoliday(date: string): void {
    this.updateCalendar(this.api.removeHoliday(date));
  }

  /** The browser's datetime-local value is interpreted in the browser's time zone. */
  protected preview(): void {
    const { start, hours } = this.previewForm.getRawValue();
    this.api.deadline(new Date(start).toISOString(), Math.round(hours * 60)).subscribe((due) => this.previewResult.set(due));
  }

  protected edit(policy: SlaPolicyInfo): void {
    this.targets.clear();
    for (const t of policy.targets) {
      this.targets.push(
        new FormGroup({
          priority: new FormControl(t.priority, { nonNullable: true }),
          firstResponseHours: new FormControl(t.firstResponseMinutes / 60, { nonNullable: true, validators: [Validators.required, Validators.min(0.25)] }),
          resolutionHours: new FormControl(t.resolutionMinutes / 60, { nonNullable: true, validators: [Validators.required, Validators.min(0.25)] }),
        }),
      );
    }
    this.policyForm.patchValue({
      threshold: policy.atRiskThresholdPercent,
      pauseOnApproval: policy.pauseStatuses.includes('PendingApproval'),
      pauseOnEmployee: policy.pauseStatuses.includes('WaitingOnEmployee'),
    });
    this.editing.set(policy.id);
  }

  protected save(policy: SlaPolicyInfo): void {
    const value = this.policyForm.getRawValue();
    this.api
      .savePolicy({
        id: policy.id,
        name: policy.name,
        isDefault: policy.isDefault,
        atRiskThresholdPercent: value.threshold,
        targets: value.targets.map((t) => ({
          priority: t.priority,
          firstResponseMinutes: Math.round(t.firstResponseHours * 60),
          resolutionMinutes: Math.round(t.resolutionHours * 60),
        })),
        pauseStatuses: [...(value.pauseOnApproval ? ['PendingApproval'] : []), ...(value.pauseOnEmployee ? ['WaitingOnEmployee'] : [])],
      })
      .subscribe({
        next: () => {
          this.editing.set(null);
          this.snackBar.open('Policy saved. It applies to cases submitted from now on.', undefined, { duration: 4000 });
          this.policies.reload();
        },
        error: (error: unknown) => this.snackBar.open(problemOf(error)?.detail ?? problemOf(error)?.title ?? 'Could not save.', 'Dismiss', { duration: 6000 }),
      });
  }

  private updateCalendar(call: Observable<unknown>, after?: () => void): void {
    call.subscribe({
      next: () => {
        after?.();
        this.calendar.reload();
      },
      error: (error: unknown) => this.snackBar.open(problemOf(error)?.detail ?? problemOf(error)?.title ?? 'Could not update the calendar.', 'Dismiss', { duration: 6000 }),
    });
  }
}
