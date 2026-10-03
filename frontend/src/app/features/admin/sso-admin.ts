import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { problemMessage } from '../../core/http/error.interceptor';

interface SsoSettings {
  isEnabled: boolean;
  displayName: string;
  authority: string;
  metadataAddress: string | null;
  clientId: string;
  hasClientSecret: boolean;
  emailDomains: string[];
  autoProvision: boolean;
  passwordLoginDisabled: boolean;
  callbackUrl: string;
}

interface ProviderInfo {
  issuer: string;
  authorizationEndpoint: string;
  tokenEndpoint: string;
}

/** HR Admin: single sign-on with an OpenID Connect provider such as Microsoft Entra ID. */
@Component({
  selector: 'app-sso-admin',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSlideToggleModule, TranslatePipe],
  template: `
    <div class="page-header">
      <div>
        <h1>{{ 'ssoAdmin.title' | translate }}</h1>
        <p>{{ 'ssoAdmin.lead' | translate }}</p>
      </div>
    </div>

    <div class="layout">
      <form class="card" [formGroup]="form" (ngSubmit)="save()" data-testid="sso-form">
        <div class="status-row">
          <mat-slide-toggle formControlName="isEnabled" data-testid="sso-enabled">
            <strong>{{ 'ssoAdmin.enabled' | translate }}</strong>
          </mat-slide-toggle>
          @if (saved()?.isEnabled) {
            <span class="pill on"><mat-icon fontSet="material-symbols-outlined">check_circle</mat-icon>{{ 'ssoAdmin.active' | translate }}</span>
          }
        </div>

        <div class="grid">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'ssoAdmin.displayName' | translate }}</mat-label>
            <input matInput formControlName="displayName" maxlength="60" />
            <mat-hint>{{ 'ssoAdmin.displayNameHint' | translate }}</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'ssoAdmin.domains' | translate }}</mat-label>
            <input matInput formControlName="emailDomains" placeholder="acme.com, acme.tn" data-testid="sso-domains" />
            <mat-hint>{{ 'ssoAdmin.domainsHint' | translate }}</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline" class="wide">
            <mat-label>{{ 'ssoAdmin.authority' | translate }}</mat-label>
            <input matInput formControlName="authority" placeholder="https://login.microsoftonline.com/<tenant-id>/v2.0" data-testid="sso-authority" />
            <mat-hint>{{ 'ssoAdmin.authorityHint' | translate }}</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'ssoAdmin.clientId' | translate }}</mat-label>
            <input matInput formControlName="clientId" data-testid="sso-client-id" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'ssoAdmin.clientSecret' | translate }}</mat-label>
            <input matInput type="password" formControlName="clientSecret" autocomplete="new-password" [placeholder]="saved()?.hasClientSecret ? '••••••••••••' : ''" data-testid="sso-secret" />
            <mat-hint>{{ (saved()?.hasClientSecret ? 'ssoAdmin.secretKept' : 'ssoAdmin.secretHint') | translate }}</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline" class="wide">
            <mat-label>{{ 'ssoAdmin.metadata' | translate }}</mat-label>
            <input matInput formControlName="metadataAddress" />
            <mat-hint>{{ 'ssoAdmin.metadataHint' | translate }}</mat-hint>
          </mat-form-field>
        </div>

        <div class="toggles">
          <mat-slide-toggle formControlName="autoProvision">
            <span class="toggle-text"><strong>{{ 'ssoAdmin.autoProvision' | translate }}</strong><small>{{ 'ssoAdmin.autoProvisionHint' | translate }}</small></span>
          </mat-slide-toggle>
          <mat-slide-toggle formControlName="passwordLoginDisabled">
            <span class="toggle-text"><strong>{{ 'ssoAdmin.required' | translate }}</strong><small>{{ 'ssoAdmin.requiredHint' | translate }}</small></span>
          </mat-slide-toggle>
        </div>

        @if (testResult(); as info) {
          <div class="result ok" data-testid="sso-test-ok">
            <mat-icon fontSet="material-symbols-outlined">check_circle</mat-icon>
            <div>
              <strong>{{ 'ssoAdmin.testOk' | translate }}</strong>
              <code>{{ info.issuer }}</code>
            </div>
          </div>
        }
        @if (error(); as message) {
          <div class="result bad" role="alert"><mat-icon fontSet="material-symbols-outlined">error</mat-icon>{{ message }}</div>
        }

        <div class="actions">
          <button mat-stroked-button type="button" (click)="test()" [disabled]="!saved() || busy()" data-testid="sso-test">
            <mat-icon fontSet="material-symbols-outlined">network_check</mat-icon> {{ 'ssoAdmin.test' | translate }}
          </button>
          <button mat-flat-button type="submit" [disabled]="form.invalid || busy()" data-testid="sso-save">{{ 'common.save' | translate }}</button>
        </div>
      </form>

      <aside class="guide">
        <div class="card">
          <h2>{{ 'ssoAdmin.redirect' | translate }}</h2>
          <p class="muted">{{ 'ssoAdmin.redirectHint' | translate }}</p>
          <div class="copy">
            <code data-testid="sso-callback">{{ callbackUrl() }}</code>
            <button mat-icon-button type="button" (click)="copy(callbackUrl())" [attr.aria-label]="'integrations.copy' | translate">
              <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>
            </button>
          </div>
        </div>
        <div class="card">
          <h2><span class="ms-logo" aria-hidden="true"><i></i><i></i><i></i><i></i></span> {{ 'ssoAdmin.entraTitle' | translate }}</h2>
          <ol>
            <li>{{ 'ssoAdmin.entra1' | translate }}</li>
            <li>{{ 'ssoAdmin.entra2' | translate }}</li>
            <li>{{ 'ssoAdmin.entra3' | translate }}</li>
            <li>{{ 'ssoAdmin.entra4' | translate }}</li>
            <li>{{ 'ssoAdmin.entra5' | translate }}</li>
          </ol>
        </div>
      </aside>
    </div>
  `,
  styles: `
    .layout {
      display: grid;
      grid-template-columns: minmax(0, 1.4fr) minmax(280px, 1fr);
      gap: 20px;
      align-items: start;
    }
    .card {
      padding: 20px 22px;
      border-radius: var(--app-radius);
      border: 1px solid var(--app-border);
      background: var(--app-card-bg);
      box-shadow: var(--app-shadow);
    }
    .guide {
      display: grid;
      gap: 16px;
    }
    .status-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 8px;
    }
    .grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 12px 14px;
    }
    .wide {
      grid-column: 1 / -1;
    }
    .toggles {
      display: grid;
      gap: 14px;
      margin: 20px 0 8px;
    }
    .toggle-text {
      display: grid;
      margin-inline-start: 8px;
    }
    .toggle-text small,
    .muted {
      color: var(--app-muted);
      font-size: 0.82rem;
    }
    .pill {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 10px;
      border-radius: 999px;
      font-size: 0.78rem;
      font-weight: 600;
    }
    .pill.on {
      color: #15803d;
      background: color-mix(in srgb, #16a34a 14%, transparent);
    }
    .pill mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .result {
      display: flex;
      gap: 10px;
      align-items: flex-start;
      padding: 12px 14px;
      margin-top: 12px;
      border-radius: 12px;
    }
    .result div {
      display: grid;
      gap: 2px;
    }
    .result.ok {
      color: #166534;
      background: color-mix(in srgb, #16a34a 12%, transparent);
    }
    .result.bad {
      color: var(--mat-sys-on-error-container);
      background: var(--mat-sys-error-container);
    }
    code {
      font-size: 0.82rem;
      overflow-wrap: anywhere;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
      margin-top: 16px;
    }
    h2 {
      display: flex;
      align-items: center;
      gap: 10px;
      font: 600 1rem Inter, 'IBM Plex Sans Arabic', sans-serif;
      margin: 0 0 6px;
    }
    .copy {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 6px 6px 6px 12px;
      border-radius: 10px;
      background: var(--mat-sys-surface-container-high);
    }
    .copy code {
      flex: 1;
    }
    ol {
      margin: 8px 0 0;
      padding-inline-start: 20px;
      display: grid;
      gap: 8px;
      font-size: 0.9rem;
      line-height: 1.45;
    }
    @media (max-width: 1099px) {
      .layout {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class SsoAdmin {
  private readonly http = inject(HttpClient);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  protected readonly saved = signal<SsoSettings | null>(null);
  protected readonly testResult = signal<ProviderInfo | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly callbackUrl = signal(`${location.origin}/api/auth/sso/callback`);

  protected readonly form = new FormGroup({
    isEnabled: new FormControl(false, { nonNullable: true }),
    displayName: new FormControl('Microsoft', { nonNullable: true, validators: [Validators.required, Validators.maxLength(60)] }),
    authority: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^https?:\/\/.+/)] }),
    metadataAddress: new FormControl('', { nonNullable: true }),
    clientId: new FormControl('', { nonNullable: true, validators: Validators.required }),
    clientSecret: new FormControl('', { nonNullable: true }),
    emailDomains: new FormControl('', { nonNullable: true, validators: Validators.required }),
    autoProvision: new FormControl(false, { nonNullable: true }),
    passwordLoginDisabled: new FormControl(false, { nonNullable: true }),
  });

  constructor() {
    this.http.get<SsoSettings | null>('/api/settings/sso').subscribe((settings) => {
      if (settings) this.apply(settings);
    });
  }

  protected save(): void {
    const value = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.testResult.set(null);
    this.http
      .put<SsoSettings>('/api/settings/sso', {
        ...value,
        metadataAddress: value.metadataAddress.trim() || null,
        clientSecret: value.clientSecret || null,
        emailDomains: value.emailDomains.split(/[\s,;]+/).filter((d) => d.length > 0),
      })
      .subscribe({
        next: (settings) => {
          this.busy.set(false);
          this.apply(settings);
          this.snackBar.open(this.translate.instant('common.saved') as string, undefined, { duration: 3000 });
        },
        error: (error: unknown) => {
          this.busy.set(false);
          this.error.set(problemMessage(this.translate, error));
        },
      });
  }

  protected test(): void {
    this.busy.set(true);
    this.error.set(null);
    this.testResult.set(null);
    this.http.post<ProviderInfo>('/api/settings/sso/test', null).subscribe({
      next: (info) => {
        this.busy.set(false);
        this.testResult.set(info);
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(this.translate, error));
      },
    });
  }

  protected copy(value: string): void {
    void navigator.clipboard?.writeText(value).then(() =>
      this.snackBar.open(this.translate.instant('integrations.copied') as string, undefined, { duration: 2000 }),
    );
  }

  private apply(settings: SsoSettings): void {
    this.saved.set(settings);
    this.callbackUrl.set(settings.callbackUrl);
    this.form.reset({
      isEnabled: settings.isEnabled,
      displayName: settings.displayName,
      authority: settings.authority,
      metadataAddress: settings.metadataAddress ?? '',
      clientId: settings.clientId,
      clientSecret: '',
      emailDomains: settings.emailDomains.join(', '),
      autoProvision: settings.autoProvision,
      passwordLoginDisabled: settings.passwordLoginDisabled,
    });
  }
}
