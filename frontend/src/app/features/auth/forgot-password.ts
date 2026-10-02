import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { AuthLayout } from './auth-layout';

/** Asks for a reset link. The confirmation is the same whether or not the address exists. */
@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, AuthLayout],
  template: `
    <app-auth-layout>
      <a routerLink="/login" class="back"><mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon> Back to sign in</a>
      @if (sent()) {
        <div class="sent" role="status" data-testid="reset-sent">
          <mat-icon fontSet="material-symbols-outlined">mark_email_read</mat-icon>
          <h1>Check your inbox</h1>
          <p>If an account exists for <strong>{{ form.controls.email.value }}</strong>, we sent a link to choose a new password. It is valid for one hour.</p>
        </div>
      } @else {
        <h1>Forgot your password?</h1>
        <p class="lead">Enter your work e-mail and we will send you a link to choose a new one.</p>
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <mat-form-field appearance="outline">
            <mat-label>Work e-mail</mat-label>
            <mat-icon matPrefix fontSet="material-symbols-outlined">mail</mat-icon>
            <input matInput type="email" formControlName="email" autocomplete="username" required />
            <mat-error>Enter a valid e-mail address.</mat-error>
          </mat-form-field>
          <button mat-flat-button type="submit" class="submit" [disabled]="sending()">Send the link</button>
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
