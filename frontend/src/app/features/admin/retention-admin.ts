import { Component, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ComplianceApi } from '../../core/api/approvals.api';
import { problemOf } from '../../core/http/error.interceptor';

/** HR Admin: how long closed cases keep personal data before the daily job anonymizes them. */
@Component({
  selector: 'app-retention-admin',
  imports: [ReactiveFormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  template: `
    <h1>Data retention</h1>
    <mat-card appearance="outlined" class="card">
      <mat-card-content>
        <p>
          Closed, cancelled and rejected cases older than this period are anonymized every night: texts, answers,
          documents and notifications are removed and the employee is replaced by "Former employee". Dates, statuses
          and SLA results are kept for statistics.
        </p>
        <div class="row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Retention (months)</mat-label>
            <input matInput type="number" min="6" max="120" [formControl]="months" data-testid="retention-months" />
            @if (months.invalid) {
              <mat-error>Between 6 and 120 months.</mat-error>
            }
          </mat-form-field>
          <button mat-flat-button type="button" (click)="save()" [disabled]="months.invalid" data-testid="save-retention">Save</button>
        </div>
        <div class="row">
          <button mat-stroked-button type="button" (click)="run()" [disabled]="running()" data-testid="run-retention">Run now</button>
          @if (lastRun() !== null) {
            <span data-testid="run-result">{{ lastRun() }} case(s) anonymized.</span>
          }
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
    .card {
      max-width: 640px;
    }
    .row {
      display: flex;
      align-items: center;
      gap: 16px;
      margin-top: 16px;
    }
  `,
})
export class RetentionAdmin {
  private readonly api = inject(ComplianceApi);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly current = rxResource({ stream: () => this.api.retention() });
  protected readonly months = new FormControl(24, { nonNullable: true, validators: [Validators.required, Validators.min(6), Validators.max(120)] });
  protected readonly running = signal(false);
  protected readonly lastRun = signal<number | null>(null);

  constructor() {
    effect(() => {
      const value = this.current.value();
      if (value) this.months.setValue(value.retentionMonths);
    });
  }

  protected save(): void {
    this.api.setRetention(this.months.value).subscribe({
      next: () => this.snackBar.open('Retention saved.', undefined, { duration: 3000 }),
      error: (error: unknown) => this.snackBar.open(problemOf(error)?.detail ?? 'Could not save.', 'Dismiss', { duration: 6000 }),
    });
  }

  protected run(): void {
    this.running.set(true);
    this.api.runRetention().subscribe({
      next: (result) => {
        this.running.set(false);
        this.lastRun.set(result.anonymized);
      },
      error: () => this.running.set(false),
    });
  }
}
