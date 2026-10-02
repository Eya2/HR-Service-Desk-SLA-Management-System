import { Component, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { problemMessage } from '../../core/http/error.interceptor';
import { AuthLayout } from './auth-layout';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

/** The password policy enforced by the API, shown as a checklist while typing. */
export const PASSWORD_RULES: { label: string; test: (p: string) => boolean }[] = [
  { label: 'auth.rule.length', test: (p) => p.length >= 12 },
  { label: 'auth.rule.upper', test: (p) => /\p{Lu}/u.test(p) },
  { label: 'auth.rule.lower', test: (p) => /\p{Ll}/u.test(p) },
  { label: 'auth.rule.digit', test: (p) => /\d/.test(p) },
  { label: 'auth.rule.symbol', test: (p) => /[^\p{L}\p{N}]/u.test(p) },
];

const policy = (control: AbstractControl): ValidationErrors | null =>
  PASSWORD_RULES.every((r) => r.test(`${control.value ?? ''}`)) ? null : { policy: true };

const matching = (group: AbstractControl): ValidationErrors | null =>
  group.get('password')?.value === group.get('confirm')?.value ? null : { mismatch: true };

/** Chooses a new password with the e-mailed link (?email=…&token=…). */
@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, AuthLayout, TranslatePipe],
  template: `
    <app-auth-layout>
      @if (done()) {
        <div role="status" data-testid="reset-done">
          <h1>{{ 'auth.changed' | translate }}</h1>
          <p class="lead">{{ 'auth.changedLead' | translate }}</p>
          <a mat-flat-button routerLink="/login" class="submit">{{ 'auth.signIn' | translate }}</a>
        </div>
      } @else if (!email() || !token()) {
        <h1>{{ 'auth.incomplete' | translate }}</h1>
        <p class="lead">{{ 'auth.incompleteLead' | translate }} <a routerLink="/forgot-password">{{ 'auth.askNew' | translate }}</a>.</p>
      } @else {
        <h1>{{ 'auth.chooseNew' | translate }}</h1>
        <p class="lead">{{ 'auth.forEmail' | translate: { email: email() } }}</p>
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'auth.newPassword' | translate }}</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="new-password" required />
          </mat-form-field>
          <ul class="rules" [attr.aria-label]="'auth.rulesLabel' | translate">
            @for (rule of rules; track rule.label) {
              <li [class.ok]="rule.test(password())">
                <mat-icon fontSet="material-symbols-outlined">{{ rule.test(password()) ? 'check_circle' : 'radio_button_unchecked' }}</mat-icon>
                {{ rule.label | translate }}
              </li>
            }
          </ul>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'auth.confirmPassword' | translate }}</mat-label>
            <input matInput type="password" formControlName="confirm" autocomplete="new-password" required />
          </mat-form-field>
          @if (form.hasError('mismatch') && form.controls.confirm.touched) {
            <p class="error" data-testid="mismatch">{{ 'auth.mismatch' | translate }}</p>
          }
          @if (error(); as message) {
            <p class="error" role="alert" data-testid="reset-error">
              {{ message }} <a routerLink="/forgot-password">{{ 'auth.askNewLink' | translate }}</a>
            </p>
          }
          <button mat-flat-button type="submit" class="submit" [disabled]="form.invalid || saving()">{{ 'auth.changePassword' | translate }}</button>
        </form>
      }
    </app-auth-layout>
  `,
  styles: `
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
  private readonly translate = inject(TranslateService);

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
        this.error.set(problemMessage(this.translate, error, 'auth.changeFailed'));
      },
    });
  }
}
