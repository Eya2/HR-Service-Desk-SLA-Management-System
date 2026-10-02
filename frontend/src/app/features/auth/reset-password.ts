import { Component, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { problemOf } from '../../core/http/error.interceptor';
import { AuthLayout } from './auth-layout';

/** The password policy enforced by the API, shown as a checklist while typing. */
export const PASSWORD_RULES: { label: string; test: (p: string) => boolean }[] = [
  { label: 'At least 12 characters', test: (p) => p.length >= 12 },
  { label: 'An upper-case letter', test: (p) => /\p{Lu}/u.test(p) },
  { label: 'A lower-case letter', test: (p) => /\p{Ll}/u.test(p) },
  { label: 'A digit', test: (p) => /\d/.test(p) },
  { label: 'A symbol', test: (p) => /[^\p{L}\p{N}]/u.test(p) },
];

const policy = (control: AbstractControl): ValidationErrors | null =>
  PASSWORD_RULES.every((r) => r.test(`${control.value ?? ''}`)) ? null : { policy: true };

const matching = (group: AbstractControl): ValidationErrors | null =>
  group.get('password')?.value === group.get('confirm')?.value ? null : { mismatch: true };

/** Chooses a new password with the e-mailed link (?email=…&token=…). */
@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, AuthLayout],
  template: `
    <app-auth-layout>
      @if (done()) {
        <div role="status" data-testid="reset-done">
          <h1>Password changed</h1>
          <p class="lead">You can now sign in with your new password. You were signed out everywhere else.</p>
          <a mat-flat-button routerLink="/login" class="submit">Sign in</a>
        </div>
      } @else if (!email() || !token()) {
        <h1>This link is incomplete</h1>
        <p class="lead">Open the link from the e-mail again, or <a routerLink="/forgot-password">ask for a new one</a>.</p>
      } @else {
        <h1>Choose a new password</h1>
        <p class="lead">For {{ email() }}</p>
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <mat-form-field appearance="outline">
            <mat-label>New password</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="new-password" required />
          </mat-form-field>
          <ul class="rules" aria-label="Password rules">
            @for (rule of rules; track rule.label) {
              <li [class.ok]="rule.test(password())">
                <mat-icon fontSet="material-symbols-outlined">{{ rule.test(password()) ? 'check_circle' : 'radio_button_unchecked' }}</mat-icon>
                {{ rule.label }}
              </li>
            }
          </ul>
          <mat-form-field appearance="outline">
            <mat-label>Confirm the password</mat-label>
            <input matInput type="password" formControlName="confirm" autocomplete="new-password" required />
          </mat-form-field>
          @if (form.hasError('mismatch') && form.controls.confirm.touched) {
            <p class="error" data-testid="mismatch">The two passwords differ.</p>
          }
          @if (error(); as message) {
            <p class="error" role="alert" data-testid="reset-error">
              {{ message }} <a routerLink="/forgot-password">Ask for a new link</a>
            </p>
          }
          <button mat-flat-button type="submit" class="submit" [disabled]="form.invalid || saving()">Change password</button>
        </form>
      }
    </app-auth-layout>
  `,
  styles: `
    h1 {
      font: 700 1.6rem/1.2 Inter, sans-serif;
      letter-spacing: -0.02em;
      margin: 0 0 6px;
    }
    .lead {
      color: var(--app-muted);
      margin: 0 0 24px;
    }
    form {
      display: grid;
    }
    .rules {
      list-style: none;
      padding: 0;
      margin: -8px 0 16px;
      display: grid;
      gap: 4px;
      font-size: 0.85rem;
      color: var(--app-muted);
    }
    .rules li {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .rules mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .rules li.ok {
      color: var(--mat-sys-primary);
    }
    .submit {
      height: 48px;
      font-weight: 600;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0 0 12px;
    }
  `,
})
export class ResetPassword {
  private readonly auth = inject(AuthService);

  /** Query parameters from the e-mailed link. */
  readonly email = input<string>();
  readonly token = input<string>();

  protected readonly rules = PASSWORD_RULES;
  protected readonly form = inject(NonNullableFormBuilder).group(
    { password: ['', [Validators.required, policy]], confirm: ['', Validators.required] },
    { validators: matching },
  );
  protected readonly password = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });
  protected readonly saving = signal(false);
  protected readonly done = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly ready = computed(() => !!this.email() && !!this.token());

  protected submit(): void {
    this.saving.set(true);
    this.error.set(null);
    this.auth.resetPassword(this.email()!, this.token()!, this.form.getRawValue().password).subscribe({
      next: () => this.done.set(true),
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(problemOf(error)?.title ?? 'The password could not be changed.');
      },
    });
  }
}
