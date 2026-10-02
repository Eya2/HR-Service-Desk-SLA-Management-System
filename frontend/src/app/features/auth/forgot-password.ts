import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { AuthLayout } from './auth-layout';
import { TranslatePipe } from '@ngx-translate/core';

/** Asks for a reset link. The confirmation is the same whether or not the address exists. */
@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink, MatIconModule, AuthLayout, TranslatePipe],
  template: `
    <app-auth-layout>
      <a routerLink="/login" class="back"><mat-icon class="flip-rtl" fontSet="material-symbols-outlined">arrow_back</mat-icon> {{ 'auth.backToSignIn' | translate }}</a>
      @if (sent()) {
        <div class="sent" role="status" data-testid="reset-sent">
          <mat-icon fontSet="material-symbols-outlined">mark_email_read</mat-icon>
          <h1>{{ 'auth.checkInbox' | translate }}</h1>
          <p [innerHTML]="'auth.sentText' | translate: { email: form.controls.email.value }"></p>
        </div>
      } @else {
        <h1>{{ 'auth.forgotTitle' | translate }}</h1>
        <p class="lead">{{ 'auth.forgotLead' | translate }}</p>
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <label class="auth-field" [class.invalid]="form.controls.email.invalid && form.controls.email.touched">
            <span class="auth-label">{{ 'auth.email' | translate }}</span>
            <span class="auth-input">
              <mat-icon fontSet="material-symbols-outlined">mail</mat-icon>
              <input type="email" formControlName="email" autocomplete="username" placeholder="name@company.com" required />
            </span>
            @if (form.controls.email.invalid && form.controls.email.touched) {
              <span class="auth-error"><mat-icon fontSet="material-symbols-outlined">error</mat-icon>{{ 'auth.emailInvalid' | translate }}</span>
            }
          </label>
          <button type="submit" class="auth-submit" [disabled]="sending()">{{ 'auth.sendLink' | translate }}</button>
        </form>
      }
    </app-auth-layout>
  `,
  styles: `
    .back {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      text-decoration: none;
      font-weight: 500;
      margin-bottom: 24px;
    }
    h1 {
      font: 700 1.6rem/1.2 Inter, 'IBM Plex Sans Arabic', sans-serif;
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
    .submit {
      height: 48px;
      font-weight: 600;
    }
    .sent mat-icon {
      font-size: 40px;
      width: 40px;
      height: 40px;
      color: var(--mat-sys-primary);
    }
  `,
})
export class ForgotPassword {
  private readonly auth = inject(AuthService);

  protected readonly form = inject(NonNullableFormBuilder).group({ email: ['', [Validators.required, Validators.email]] });
  protected readonly sending = signal(false);
  protected readonly sent = signal(false);

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.sending.set(true);
    // Whatever happens, show the same confirmation: it must not reveal whether the account exists.
    this.auth.requestPasswordReset(this.form.getRawValue().email).subscribe({
      next: () => this.sent.set(true),
      error: () => this.sent.set(true),
    });
  }
}
