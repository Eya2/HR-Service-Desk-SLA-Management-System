import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { problemOf } from '../../core/http/error.interceptor';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <main class="page">
      <mat-card class="card" appearance="outlined">
        <div class="brand">
          <mat-icon fontSet="material-symbols-outlined">support_agent</mat-icon>
          <span>HR Service Desk</span>
        </div>
        <h1>Sign in</h1>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <mat-form-field appearance="outline">
            <mat-label>Work e-mail</mat-label>
            <input matInput formControlName="email" type="email" autocomplete="username" required />
            @if (form.controls.email.hasError('required')) {
              <mat-error>E-mail is required.</mat-error>
            } @else if (form.controls.email.hasError('email')) {
              <mat-error>Enter a valid e-mail address.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Password</mat-label>
            <input
              matInput
              formControlName="password"
              [type]="showPassword() ? 'text' : 'password'"
              autocomplete="current-password"
              required
            />
            <button
              mat-icon-button
              matSuffix
              type="button"
              (click)="showPassword.set(!showPassword())"
              [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'"
            >
              <mat-icon fontSet="material-symbols-outlined">{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
            </button>
            @if (form.controls.password.hasError('required')) {
              <mat-error>Password is required.</mat-error>
            }
          </mat-form-field>

          @if (error(); as message) {
            <p class="error" role="alert" data-testid="login-error">{{ message }}</p>
          }

          <button mat-flat-button type="submit" [disabled]="submitting()">
            @if (submitting()) {
              <mat-spinner diameter="20" />
            } @else {
              Sign in
            }
          </button>
        </form>
      </mat-card>
    </main>
  `,
  styles: `
    .page {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 16px;
      box-sizing: border-box;
      background: var(--mat-sys-surface-container);
    }
    .card {
      width: 100%;
      max-width: 400px;
      padding: 32px 24px;
      box-sizing: border-box;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 8px;
      color: var(--mat-sys-primary);
      font: var(--mat-sys-title-medium);
    }
    h1 {
      font: var(--mat-sys-headline-small);
      margin: 16px 0 24px;
    }
    form {
      display: grid;
      gap: 4px;
    }
    button[type='submit'] {
      height: 44px;
      margin-top: 8px;
    }
    button[type='submit'] mat-spinner {
      margin: 0 auto;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0 0 8px;
    }
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Bound from the `returnUrl` query parameter set by the auth guard. */
  readonly returnUrl = input<string>();

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  protected readonly submitting = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();

    this.auth.login(email, password).subscribe({
      next: () => void this.router.navigateByUrl(this.safeReturnUrl()),
      error: (error: unknown) => {
        this.submitting.set(false);
        this.error.set(problemOf(error)?.title ?? 'Sign-in failed. Please try again.');
      },
    });
  }

  /** Only same-app paths are accepted, so the login page cannot be used as an open redirect. */
  private safeReturnUrl(): string {
    const url = this.returnUrl();
    return url && url.startsWith('/') && !url.startsWith('//') ? url : '/';
  }
}
