import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Observable } from 'rxjs';
import { ApiKeyInfo, IntegrationsApi, WebhookInfo } from '../../core/api/integrations.api';
import { problemMessage } from '../../core/http/error.interceptor';

/** A secret shown once, right after it was created. */
interface Revealed {
  title: string;
  value: string;
}

/** HR Admin: API keys for external systems and webhook subscriptions with their delivery history. */
@Component({
  selector: 'app-integrations-admin',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSlideToggleModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  template: `
    <div class="page-header">
      <div>
        <h1>{{ 'integrations.title' | translate }}</h1>
        <p>{{ 'integrations.lead' | translate }}</p>
      </div>
      <a mat-stroked-button href="/swagger/index.html?urls.primaryName=Integration%20API" target="_blank" rel="noopener">
        <mat-icon fontSet="material-symbols-outlined">menu_book</mat-icon> {{ 'integrations.docs' | translate }}
      </a>
    </div>

    @if (revealed(); as secret) {
      <section class="reveal" role="alert" data-testid="secret">
        <mat-icon fontSet="material-symbols-outlined">key</mat-icon>
        <div class="reveal-text">
          <strong>{{ secret.title }}</strong>
          <span>{{ 'integrations.copyNow' | translate }}</span>
          <code>{{ secret.value }}</code>
        </div>
        <button mat-flat-button type="button" (click)="copy(secret.value)">
          <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon> {{ 'integrations.copy' | translate }}
        </button>
        <button mat-icon-button type="button" (click)="revealed.set(null)" [attr.aria-label]="'common.close' | translate">
          <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
        </button>
      </section>
    }

    <section class="card">
      <header class="card-head">
        <div>
          <h2>{{ 'integrations.keys' | translate }}</h2>
          <p>{{ 'integrations.keysLead' | translate }}</p>
        </div>
      </header>

      <form class="inline" [formGroup]="keyForm" (ngSubmit)="createKey()">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ 'integrations.keyName' | translate }}</mat-label>
          <input matInput formControlName="name" maxlength="100" data-testid="key-name" />
        </mat-form-field>
        <mat-checkbox formControlName="read">tickets:read</mat-checkbox>
        <mat-checkbox formControlName="write">tickets:write</mat-checkbox>
        <button mat-flat-button type="submit" [disabled]="keyForm.invalid || (!keyForm.value.read && !keyForm.value.write)" data-testid="create-key">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon> {{ 'integrations.createKey' | translate }}
        </button>
      </form>

      <table class="data">
        <thead>
          <tr>
            <th>{{ 'integrations.name' | translate }}</th>
            <th>{{ 'integrations.key' | translate }}</th>
            <th>{{ 'integrations.scopes' | translate }}</th>
            <th>{{ 'integrations.lastUsed' | translate }}</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (k of keys.value() ?? []; track k.id) {
            <tr [class.revoked]="k.revokedAt" [attr.data-testid]="'key-' + k.name">
              <td>{{ k.name }}</td>
              <td><code>hrd_{{ k.prefix }}_…</code></td>
              <td>
                @for (s of k.scopes; track s) {
                  <span class="pill">{{ s }}</span>
                }
              </td>
              <td>{{ k.lastUsedAt ? (k.lastUsedAt | date: 'short') : ('integrations.never' | translate) }}</td>
              <td class="end">
                @if (k.revokedAt) {
                  <span class="pill off">{{ 'integrations.revoked' | translate }}</span>
                } @else {
                  <button mat-button type="button" class="danger" (click)="revoke(k)" [attr.data-testid]="'revoke-' + k.name">
                    {{ 'integrations.revoke' | translate }}
                  </button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="5" class="empty">{{ 'integrations.noKeys' | translate }}</td></tr>
          }
        </tbody>
      </table>
    </section>

    <section class="card">
      <header class="card-head">
        <div>
          <h2>{{ 'integrations.webhooks' | translate }}</h2>
          <p>{{ 'integrations.webhooksLead' | translate }}</p>
        </div>
        @if (!editing()) {
          <button mat-stroked-button type="button" (click)="editWebhook(null)" data-testid="new-webhook">
            <mat-icon fontSet="material-symbols-outlined">add</mat-icon> {{ 'integrations.newWebhook' | translate }}
          </button>
        }
      </header>

      @if (editing()) {
        <form class="webhook-form" [formGroup]="webhookForm" (ngSubmit)="saveWebhook()" data-testid="webhook-form">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'integrations.name' | translate }}</mat-label>
            <input matInput formControlName="name" maxlength="100" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'integrations.url' | translate }}</mat-label>
            <input matInput formControlName="url" maxlength="500" placeholder="https://" data-testid="webhook-url" />
          </mat-form-field>
          <fieldset>
            <legend>{{ 'integrations.events' | translate }}</legend>
            @for (e of events(); track e) {
              <mat-checkbox [checked]="selectedEvents().includes(e)" (change)="toggleEvent(e, $event.checked)">
                <code>{{ e }}</code> — {{ 'integrations.event.' + e | translate }}
              </mat-checkbox>
            }
          </fieldset>
          <mat-slide-toggle formControlName="isActive">{{ 'integrations.active' | translate }}</mat-slide-toggle>
          @if (editingId()) {
            <mat-checkbox formControlName="rotateSecret">{{ 'integrations.rotate' | translate }}</mat-checkbox>
          }
          <div class="actions">
            <button mat-button type="button" (click)="editing.set(false)">{{ 'common.cancel' | translate }}</button>
            <button mat-flat-button type="submit" [disabled]="webhookForm.invalid || selectedEvents().length === 0" data-testid="save-webhook">
              {{ 'common.save' | translate }}
            </button>
          </div>
        </form>
      }

      <div class="webhooks">
        @for (w of webhooks.value() ?? []; track w.id) {
          <article class="webhook" [attr.data-testid]="'webhook-' + w.name">
            <div class="webhook-head">
              <span class="dot" [class.on]="w.isActive"></span>
              <div class="webhook-text">
                <strong>{{ w.name }}</strong>
                <code>{{ w.url }}</code>
                <span class="events">
                  @for (e of w.events; track e) {
                    <span class="pill">{{ e }}</span>
                  }
                </span>
              </div>
              <div class="stats">
                @if (w.failedDeliveries > 0) {
                  <span class="pill bad">{{ 'integrations.failedCount' | translate: { count: w.failedDeliveries } }}</span>
                }
                @if (w.pendingDeliveries > 0) {
                  <span class="pill warn">{{ 'integrations.pendingCount' | translate: { count: w.pendingDeliveries } }}</span>
                }
                @if (w.lastDeliveredAt) {
                  <span class="muted">{{ 'integrations.lastDelivered' | translate: { date: (w.lastDeliveredAt | date: 'short') } }}</span>
                }
              </div>
            </div>
            <div class="webhook-actions">
              <button mat-button type="button" (click)="toggleDeliveries(w)" [attr.data-testid]="'deliveries-' + w.name">
                <mat-icon fontSet="material-symbols-outlined">history</mat-icon> {{ 'integrations.deliveries' | translate }}
              </button>
              <button mat-button type="button" (click)="ping(w)" [attr.data-testid]="'ping-' + w.name">
                <mat-icon fontSet="material-symbols-outlined">network_ping</mat-icon> {{ 'integrations.ping' | translate }}
              </button>
              <button mat-button type="button" (click)="editWebhook(w)">
                <mat-icon fontSet="material-symbols-outlined">edit</mat-icon> {{ 'common.edit' | translate }}
              </button>
              <button mat-button type="button" class="danger" (click)="deleteWebhook(w)">
                <mat-icon fontSet="material-symbols-outlined">delete</mat-icon> {{ 'common.delete' | translate }}
              </button>
            </div>

            @if (openDeliveries() === w.id) {
              <table class="data deliveries" data-testid="delivery-table">
                <thead>
                  <tr>
                    <th>{{ 'integrations.when' | translate }}</th>
                    <th>{{ 'integrations.eventCol' | translate }}</th>
                    <th>{{ 'integrations.status' | translate }}</th>
                    <th>{{ 'integrations.attempts' | translate }}</th>
                    <th>{{ 'integrations.response' | translate }}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  @for (d of deliveries.value()?.items ?? []; track d.id) {
                    <tr>
                      <td>{{ d.createdAt | date: 'short' }}</td>
                      <td>
                        <code>{{ d.eventType }}</code>
                        @if (d.ticketId) {
                          <a [routerLink]="['/tickets', d.ticketId]" class="case-link">{{ 'integrations.case' | translate }}</a>
                        }
                      </td>
                      <td><span class="pill" [class.ok]="d.status === 'Succeeded'" [class.bad]="d.status === 'Failed'" [class.warn]="d.status === 'Pending'">{{ 'integrations.deliveryStatus.' + d.status | translate }}</span></td>
                      <td>{{ d.attempts }}</td>
                      <td class="response" [matTooltip]="d.lastError ?? ''">
                        {{ d.lastStatusCode ?? '—' }}
                        @if (d.nextAttemptAt && d.attempts > 0) {
                          <small>{{ 'integrations.nextAttempt' | translate: { date: (d.nextAttemptAt | date: 'shortTime') } }}</small>
                        }
                      </td>
                      <td class="end">
                        @if (d.status !== 'Pending') {
                          <button mat-button type="button" (click)="redeliver(d.id)">{{ 'integrations.redeliver' | translate }}</button>
                        }
                      </td>
                    </tr>
                  } @empty {
                    <tr><td colspan="6" class="empty">{{ 'integrations.noDeliveries' | translate }}</td></tr>
                  }
                </tbody>
              </table>
            }
          </article>
        } @empty {
          <p class="empty">{{ 'integrations.noWebhooks' | translate }}</p>
        }
      </div>
    </section>
  `,
  styles: `
    .card {
      padding: 20px 22px;
      margin-bottom: 20px;
      border-radius: var(--app-radius);
      border: 1px solid var(--app-border);
      background: var(--app-card-bg);
    }
    .card-head {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 16px;
      margin-bottom: 12px;
    }
    .card-head h2 {
      font: 600 1.1rem Inter, 'IBM Plex Sans Arabic', sans-serif;
      margin: 0 0 4px;
    }
    .card-head p,
    .muted,
    .empty {
      color: var(--app-muted);
      margin: 0;
    }
    .inline {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
    }
    .inline mat-form-field {
      min-width: 260px;
    }
    table.data {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.9rem;
    }
    table.data th {
      text-align: start;
      font-weight: 600;
      color: var(--app-muted);
      padding: 8px;
      border-bottom: 1px solid var(--app-border);
    }
    table.data td {
      padding: 8px;
      border-bottom: 1px solid var(--app-border);
      vertical-align: middle;
    }
    tr.revoked td {
      color: var(--app-muted);
      text-decoration: line-through;
    }
    tr.revoked td.end {
      text-decoration: none;
    }
    .end {
      text-align: end;
    }
    code {
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      font-size: 0.85em;
    }
    .pill {
      display: inline-block;
      margin-inline-end: 4px;
      padding: 1px 8px;
      border-radius: 10px;
      font-size: 0.78rem;
      font-weight: 600;
      background: var(--mat-sys-surface-container-high);
    }
    .pill.ok {
      color: var(--mat-sys-on-primary-container);
      background: var(--mat-sys-primary-container);
    }
    .pill.warn {
      color: var(--mat-sys-on-tertiary-container);
      background: var(--mat-sys-tertiary-container);
    }
    .pill.bad,
    .pill.off {
      color: var(--mat-sys-on-error-container);
      background: var(--mat-sys-error-container);
    }
    .danger {
      color: var(--mat-sys-error);
    }
    .reveal {
      display: flex;
      align-items: center;
      gap: 14px;
      padding: 16px 18px;
      margin-bottom: 20px;
      border-radius: 14px;
      border: 1px solid color-mix(in srgb, var(--mat-sys-tertiary) 40%, transparent);
      background: color-mix(in srgb, var(--mat-sys-tertiary) 10%, var(--app-card-bg));
    }
    .reveal-text {
      display: grid;
      gap: 4px;
      flex: 1;
      min-width: 0;
    }
    .reveal-text span {
      color: var(--app-muted);
      font-size: 0.85rem;
    }
    .reveal-text code {
      padding: 6px 8px;
      border-radius: 8px;
      overflow-wrap: anywhere;
      background: var(--mat-sys-surface-container-high);
    }
    .webhook-form {
      display: grid;
      gap: 10px;
      padding: 16px;
      margin-bottom: 16px;
      border-radius: 12px;
      background: var(--mat-sys-surface-container-low);
    }
    fieldset {
      display: grid;
      gap: 2px;
      border: 1px solid var(--app-border);
      border-radius: 10px;
      padding: 8px 12px;
      margin: 0;
    }
    legend {
      color: var(--app-muted);
      font-size: 0.85rem;
      padding: 0 4px;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
    .webhooks {
      display: grid;
      gap: 12px;
    }
    .webhook {
      padding: 14px 16px;
      border-radius: 12px;
      border: 1px solid var(--app-border);
    }
    .webhook-head {
      display: flex;
      gap: 12px;
      align-items: flex-start;
    }
    .webhook-text {
      display: grid;
      gap: 4px;
      flex: 1;
      min-width: 0;
    }
    .webhook-text code {
      color: var(--app-muted);
      overflow-wrap: anywhere;
    }
    .dot {
      width: 10px;
      height: 10px;
      margin-top: 6px;
      border-radius: 50%;
      flex: none;
      background: var(--app-muted);
    }
    .dot.on {
      background: #16a34a;
      box-shadow: 0 0 0 4px color-mix(in srgb, #16a34a 20%, transparent);
    }
    .stats {
      display: flex;
      gap: 6px;
      align-items: center;
      flex-wrap: wrap;
      justify-content: flex-end;
    }
    .webhook-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
      margin-top: 8px;
    }
    .deliveries {
      margin-top: 8px;
    }
    .case-link {
      margin-inline-start: 8px;
      font-size: 0.8rem;
    }
    .response small {
      display: block;
      color: var(--app-muted);
    }
  `,
})
export class IntegrationsAdmin {
  private readonly api = inject(IntegrationsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  protected readonly keys = rxResource({ stream: () => this.api.keys() });
  protected readonly webhooks = rxResource({ stream: () => this.api.webhooks() });
  private readonly catalog = rxResource({ stream: () => this.api.catalog() });
  protected readonly events = () => this.catalog.value()?.events ?? [];

  protected readonly revealed = signal<Revealed | null>(null);
  protected readonly editing = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly selectedEvents = signal<string[]>([]);
  protected readonly openDeliveries = signal<string | null>(null);
  protected readonly deliveries = rxResource({
    params: () => this.openDeliveries() ?? undefined,
    stream: ({ params }) => this.api.deliveries(params),
  });

  protected readonly keyForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    read: new FormControl(true, { nonNullable: true }),
    write: new FormControl(false, { nonNullable: true }),
  });

  protected readonly webhookForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    url: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500), Validators.pattern(/^https?:\/\/.+/)] }),
    isActive: new FormControl(true, { nonNullable: true }),
    rotateSecret: new FormControl(false, { nonNullable: true }),
  });

  protected createKey(): void {
    const { name, read, write } = this.keyForm.getRawValue();
    const scopes = [...(read ? ['tickets:read'] : []), ...(write ? ['tickets:write'] : [])];
    this.run(this.api.createKey(name, scopes), (created) => {
      this.revealed.set({ title: this.t('integrations.keyCreated', { name: created.key.name }), value: created.secret });
      this.keyForm.reset({ name: '', read: true, write: false });
      this.keys.reload();
    });
  }

  protected revoke(key: ApiKeyInfo): void {
    if (!confirm(this.t('integrations.confirmRevoke', { name: key.name }))) return;
    this.run(this.api.revokeKey(key.id), () => this.keys.reload());
  }

  protected editWebhook(webhook: WebhookInfo | null): void {
    this.editingId.set(webhook?.id ?? null);
    this.webhookForm.reset({ name: webhook?.name ?? '', url: webhook?.url ?? 'https://', isActive: webhook?.isActive ?? true, rotateSecret: false });
    this.selectedEvents.set(webhook ? [...webhook.events] : ['ticket.status_changed']);
    this.editing.set(true);
  }

  protected toggleEvent(event: string, checked: boolean): void {
    this.selectedEvents.update((list) => (checked ? [...new Set([...list, event])] : list.filter((e) => e !== event)));
  }

  protected saveWebhook(): void {
    const value = this.webhookForm.getRawValue();
    this.run(this.api.saveWebhook(this.editingId(), { ...value, events: this.selectedEvents() }), (saved) => {
      if (saved.secret) this.revealed.set({ title: this.t('integrations.secretFor', { name: saved.webhook.name }), value: saved.secret });
      this.editing.set(false);
      this.webhooks.reload();
    });
  }

  protected deleteWebhook(webhook: WebhookInfo): void {
    if (!confirm(this.t('integrations.confirmDelete', { name: webhook.name }))) return;
    this.run(this.api.deleteWebhook(webhook.id), () => this.webhooks.reload());
  }

  protected toggleDeliveries(webhook: WebhookInfo): void {
    this.openDeliveries.update((id) => (id === webhook.id ? null : webhook.id));
  }

  protected ping(webhook: WebhookInfo): void {
    this.run(this.api.ping(webhook.id), () => {
      this.snackBar.open(this.t('integrations.pingQueued'), undefined, { duration: 3000 });
      this.openDeliveries.set(webhook.id);
      this.deliveries.reload();
      this.webhooks.reload();
    });
  }

  protected redeliver(deliveryId: string): void {
    this.run(this.api.redeliver(deliveryId), () => {
      this.deliveries.reload();
      this.webhooks.reload();
    });
  }

  protected copy(value: string): void {
    void navigator.clipboard?.writeText(value).then(() => this.snackBar.open(this.t('integrations.copied'), undefined, { duration: 2000 }));
  }

  private run<T>(call: Observable<T>, next: (value: T) => void): void {
    call.subscribe({
      next,
      error: (error: unknown) =>
        this.snackBar.open(problemMessage(this.translate, error), this.t('common.dismiss'), { duration: 6000 }),
    });
  }

  private t(key: string, params?: Record<string, unknown>): string {
    return this.translate.instant(key, params) as string;
  }
}
