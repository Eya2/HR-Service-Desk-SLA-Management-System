import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { problemOf } from '../../core/http/error.interceptor';
import { AuthLayout } from './auth-layout';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    AuthLayout,
  ],
  template: `
    <app-auth-layout>
      <h1>Welcome back</h1>
      <p class="lead">Sign in with your work e-mail.</p>

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>Work e-mail</mat-label>
          <mat-icon matPrefix fontSet="material-symbols-outlined">mail</mat-icon>
          <input matInput formControlName="email" type="email" autocomplete="username" required />
          @if (form.controls.email.hasError('required')) {
            <mat-error>E-mail is required.</mat-error>
          } @else if (form.controls.email.hasError('email')) {
            <mat-error>Enter a valid e-mail address.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Password</mat-label>
          <mat-icon matPrefix fontSet="material-symbols-outlined">lock</mat-icon>
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

        <div class="row">
          <mat-checkbox formControlName="rememberMe" data-testid="remember-me">Remember me</mat-checkbox>
          <a routerLink="/forgot-password" data-testid="forgot-link">Forgot password?</a>
        </div>

        @if (error(); as message) {
          <p class="error" role="alert" data-testid="login-error">
            <mat-icon fontSet="material-symbols-outlined">error</mat-icon>{{ message }}
          </p>
        }

        <button mat-flat-button type="submit" class="submit" [disabled]="submitting()">
          @if (submitting()) {
            <mat-spinner diameter="20" />
          } @else {
            Sign in
          }
        </button>
      </form>
      <p class="hint">On a shared computer, leave "Remember me" unticked: you will be signed out when the browser closes.</p>
    </app-auth-layout>
  `,
  styles: `
    h1 {
      font: 700 1.8rem/1.2 Inter, sans-serif;
      letter-spacing: -0.02em;
      margin: 0 0 6px;
    }
    .lead,
    .hint {
      color: var(--app-muted);
      margin: 0 0 24px;
    }
    .hint {
      font-size: 0.8rem;
      margin: 20px 0 0;
    }
    form {
      display: grid;
      gap: 4px;
    }
    .row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin: -4px 0 12px;
    }
    .row a {
      font-weight: 500;
      text-decoration: none;
    }
    .submit {
      height: 48px;
      font-weight: 600;
    }
    .submit mat-spinner {
      margin: 0 auto;
    }
    .error {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 12px;
      border-radius: 10px;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
      margin: 0 0 12px;
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
    rememberMe: [false],
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
    const { email, password, rememberMe } = this.form.getRawValue();

    this.auth.login(email, password, rememberMe).subscribe({
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
