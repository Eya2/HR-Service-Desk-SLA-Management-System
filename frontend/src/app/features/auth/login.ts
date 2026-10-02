import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { problemMessage } from '../../core/http/error.interceptor';
import { AuthLayout } from './auth-layout';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCheckboxModule,
    MatIconModule,
    AuthLayout,
    TranslatePipe,
  ],
  template: `
    <app-auth-layout>
      <h1>{{ 'auth.welcome' | translate }}</h1>
      <p class="lead">{{ 'auth.lead' | translate }}</p>

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <label class="auth-field" [class.invalid]="form.controls.email.invalid && form.controls.email.touched">
          <span class="auth-label">{{ 'auth.email' | translate }}</span>
          <span class="auth-input">
            <mat-icon fontSet="material-symbols-outlined">mail</mat-icon>
            <input formControlName="email" type="email" autocomplete="username" placeholder="name@company.com" required />
          </span>
          @if (form.controls.email.touched && form.controls.email.hasError('required')) {
            <span class="auth-error"><mat-icon fontSet="material-symbols-outlined">error</mat-icon>{{ 'auth.emailRequired' | translate }}</span>
          } @else if (form.controls.email.touched && form.controls.email.hasError('email')) {
            <span class="auth-error"><mat-icon fontSet="material-symbols-outlined">error</mat-icon>{{ 'auth.emailInvalid' | translate }}</span>
          }
        </label>

        <div class="auth-field" [class.invalid]="form.controls.password.invalid && form.controls.password.touched">
          <span class="auth-label">
            <label for="login-password">{{ 'auth.password' | translate }}</label>
            <a routerLink="/forgot-password" data-testid="forgot-link">{{ 'auth.forgotLink' | translate }}</a>
          </span>
          <span class="auth-input">
            <mat-icon fontSet="material-symbols-outlined">lock</mat-icon>
            <input
              id="login-password"
              formControlName="password"
              [type]="showPassword() ? 'text' : 'password'"
              autocomplete="current-password"
              placeholder="••••••••••••"
              required
            />
            <button
              type="button"
              class="reveal"
              (click)="showPassword.set(!showPassword())"
              [attr.aria-label]="(showPassword() ? 'auth.hidePassword' : 'auth.showPassword') | translate"
            >
              <mat-icon fontSet="material-symbols-outlined">{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
            </button>
          </span>
          @if (form.controls.password.touched && form.controls.password.hasError('required')) {
            <span class="auth-error"><mat-icon fontSet="material-symbols-outlined">error</mat-icon>{{ 'auth.passwordRequired' | translate }}</span>
          }
        </div>

        <mat-checkbox formControlName="rememberMe" class="remember" data-testid="remember-me">{{ 'auth.rememberMe' | translate }}</mat-checkbox>

        @if (error(); as message) {
          <p class="error" role="alert" data-testid="login-error">
            <mat-icon fontSet="material-symbols-outlined">error</mat-icon>{{ message }}
          </p>
        }

        <button type="submit" class="auth-submit" [disabled]="submitting()">
          @if (submitting()) {
            <span class="auth-spinner" aria-hidden="true"></span>
          } @else {
            {{ 'auth.signIn' | translate }}
            <mat-icon class="flip-rtl" fontSet="material-symbols-outlined">arrow_forward</mat-icon>
          }
        </button>
      </form>
      <p class="hint">{{ 'auth.sharedHint' | translate }}</p>
    </app-auth-layout>
  `,
  styles: `
    h1 {
      font: 700 2rem/1.15 Inter, 'IBM Plex Sans Arabic', sans-serif;
      letter-spacing: -0.03em;
      margin: 0 0 8px;
    }
    .lead {
      color: var(--app-muted);
      margin: 0 0 28px;
      font-size: 1rem;
    }
    .hint {
      color: var(--app-muted);
      font-size: 0.8rem;
      line-height: 1.5;
      margin: 20px 0 0;
      text-align: center;
    }
    form {
      display: grid;
    }
    .remember {
      margin: -4px 0 16px -8px;
    }
    .error {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 12px;
      border-radius: 12px;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
      margin: 0 0 16px;
      font-size: 0.9rem;
    }
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

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
        this.error.set(problemMessage(this.translate, error, 'auth.signInFailed'));
      },
    });
  }

  /** Only same-app paths are accepted, so the login page cannot be used as an open redirect. */
  private safeReturnUrl(): string {
    const url = this.returnUrl();
    return url && url.startsWith('/') && !url.startsWith('//') ? url : '/';
  }
}
