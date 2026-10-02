import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { ASSIGNABLE_ROLES, UserSummary, UsersApi } from '../../core/api/users.api';
import { problemMessage } from '../../core/http/error.interceptor';
import { EnumLabelPipe } from '../../shared/ui/enum-label';
import { PASSWORD_RULES } from '../auth/reset-password';

/** HR Admin: the organisation's accounts — roles, manager, activation and password reset. */
@Component({
  selector: 'app-users-admin',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatSlideToggleModule,
    TranslatePipe,
    EnumLabelPipe,
  ],
  template: `
    <div class="page-header">
      <div>
        <h1>{{ 'users.title' | translate }}</h1>
        <p>{{ 'users.lead' | translate }}</p>
      </div>
      <button mat-flat-button type="button" (click)="startCreate()" data-testid="new-user">
        <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon> {{ 'users.new' | translate }}
      </button>
    </div>

    <div class="filters dense-controls">
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <mat-label>{{ 'users.search' | translate }}</mat-label>
        <input matInput [value]="search()" (input)="search.set($any($event.target).value); page.set(0)" data-testid="user-search" />
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ 'users.role' | translate }}</mat-label>
        <mat-select [value]="role()" (selectionChange)="role.set($event.value); page.set(0)">
          <mat-option [value]="null">{{ 'common.all' | translate }}</mat-option>
          @for (r of roles; track r) {
            <mat-option [value]="r">{{ r | enumLabel: 'role' }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>

    <div class="layout" [class.with-editor]="open()">
      <div class="list">
        <table class="data">
          <thead>
            <tr>
              <th>{{ 'users.name' | translate }}</th>
              <th>{{ 'users.roles' | translate }}</th>
              <th>{{ 'users.lastLogin' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (u of users.value()?.items ?? []; track u.id) {
              <tr (click)="edit(u)" [class.selected]="editingId() === u.id" [class.inactive]="!u.isActive" [attr.data-testid]="'user-' + u.email">
                <td>
                  <div class="person">
                    <span class="avatar" aria-hidden="true">{{ initials(u.fullName) }}</span>
                    <span class="who">
                      <strong>{{ u.fullName }}</strong>
                      <small [title]="u.email">{{ u.email }}</small>
                    </span>
                  </div>
                </td>
                <td>
                  @for (r of u.roles; track r) {
                    <span class="pill">{{ r | enumLabel: 'role' }}</span>
                  }
                  @if (!u.isActive) {
                    <span class="pill off">{{ 'users.inactive' | translate }}</span>
                  }
                </td>
                <td class="muted">{{ u.lastLoginAt ? (u.lastLoginAt | date: 'short') : ('integrations.never' | translate) }}</td>
              </tr>
            }
          </tbody>
        </table>
        @if (users.value(); as result) {
          <mat-paginator [length]="result.totalCount" [pageIndex]="page()" [pageSize]="20" (page)="onPage($event)" />
        }
      </div>

      @if (open()) {
        <form class="editor" [formGroup]="form" (ngSubmit)="save()" data-testid="user-editor">
          <h2>{{ (editingId() ? 'users.edit' : 'users.new') | translate }}</h2>
          <div class="two">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'users.firstName' | translate }}</mat-label>
              <input matInput formControlName="firstName" maxlength="100" data-testid="first-name" />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'users.lastName' | translate }}</mat-label>
              <input matInput formControlName="lastName" maxlength="100" data-testid="last-name" />
            </mat-form-field>
          </div>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'auth.email' | translate }}</mat-label>
            <input matInput type="email" formControlName="email" data-testid="email" />
          </mat-form-field>
          <fieldset>
            <legend>{{ 'users.roles' | translate }}</legend>
            @for (r of roles; track r) {
              <mat-checkbox [checked]="selectedRoles().includes(r)" (change)="toggleRole(r, $event.checked)" [attr.data-testid]="'role-' + r">
                {{ r | enumLabel: 'role' }}
              </mat-checkbox>
            }
          </fieldset>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'users.manager' | translate }}</mat-label>
            <mat-select formControlName="managerId">
              <mat-option [value]="null">{{ 'common.none' | translate }}</mat-option>
              @for (m of managers(); track m.id) {
                <mat-option [value]="m.id">{{ m.fullName }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (editingId()) {
            <mat-slide-toggle formControlName="isActive">{{ 'users.active' | translate }}</mat-slide-toggle>
          }
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ (editingId() ? 'users.newPassword' : 'auth.password') | translate }}</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="new-password" data-testid="password" />
            <mat-hint>{{ (editingId() ? 'users.passwordHintEdit' : 'users.passwordHint') | translate }}</mat-hint>
          </mat-form-field>
          <div class="actions">
            <button mat-button type="button" (click)="open.set(false)">{{ 'common.cancel' | translate }}</button>
            <button mat-flat-button type="submit" [disabled]="form.invalid || selectedRoles().length === 0 || saving()" data-testid="save-user">
              {{ 'common.save' | translate }}
            </button>
          </div>
        </form>
      }
    </div>
  `,
  styles: `
    .filters {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
      margin-bottom: 16px;
    }
    .search {
      min-width: 280px;
    }
    .layout {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: 20px;
      align-items: start;
    }
    .layout.with-editor {
      grid-template-columns: minmax(0, 1.5fr) minmax(340px, 1fr);
    }
    .list {
      border-radius: var(--app-radius);
      border: 1px solid var(--app-border);
      background: var(--app-card-bg);
      overflow: hidden;
    }
    table.data {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.9rem;
    }
    th {
      text-align: start;
      padding: 10px 12px;
      color: var(--app-muted);
      font-weight: 600;
      border-bottom: 1px solid var(--app-border);
    }
    td {
      padding: 8px 12px;
      border-bottom: 1px solid var(--app-border);
      vertical-align: middle;
    }
    tbody tr {
      cursor: pointer;
    }
    tbody tr:hover,
    tr.selected {
      background: color-mix(in srgb, var(--mat-sys-primary) 6%, transparent);
    }
    tr.inactive strong {
      color: var(--app-muted);
    }
    table.data {
      table-layout: fixed;
    }
    th:nth-child(2) {
      width: 34%;
    }
    th:nth-child(3) {
      width: 150px;
      white-space: nowrap;
    }
    .person {
      display: flex;
      align-items: center;
      gap: 10px;
      min-width: 0;
    }
    .avatar {
      flex: none;
      width: 32px;
      height: 32px;
      border-radius: 50%;
      display: grid;
      place-items: center;
      font: 600 0.75rem/1 Inter, 'IBM Plex Sans Arabic', sans-serif;
      color: #fff;
      background: var(--app-brand-gradient);
    }
    .who {
      display: grid;
      min-width: 0;
    }
    .who strong,
    .who small {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .who small,
    .muted {
      color: var(--app-muted);
    }
    .pill {
      display: inline-block;
      margin: 1px 4px 1px 0;
      padding: 1px 8px;
      border-radius: 10px;
      font-size: 0.75rem;
      font-weight: 600;
      white-space: nowrap;
      background: var(--mat-sys-surface-container-high);
    }
    .pill.off {
      color: var(--mat-sys-on-error-container);
      background: var(--mat-sys-error-container);
    }
    .editor {
      display: grid;
      gap: 10px;
      padding: 20px;
      border-radius: 16px;
      border: 1px solid var(--app-border);
      background: var(--app-card-bg);
      position: sticky;
      top: 72px;
    }
    .editor h2 {
      font: 600 1.1rem Inter, 'IBM Plex Sans Arabic', sans-serif;
      margin: 0;
    }
    .two {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 10px;
    }
    fieldset {
      display: grid;
      grid-template-columns: 1fr 1fr;
      border: 1px solid var(--app-border);
      border-radius: 10px;
      padding: 6px 10px;
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
    @media (max-width: 959px) {
      .layout.with-editor {
        grid-template-columns: 1fr;
      }
      .editor {
        position: static;
      }
    }
  `,
})
export class UsersAdmin {
  private readonly api = inject(UsersApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  protected readonly roles = ASSIGNABLE_ROLES;
  protected readonly search = signal('');
  protected readonly role = signal<string | null>(null);
  protected readonly page = signal(0);
  private readonly debouncedSearch = toSignal(toObservable(this.search).pipe(debounceTime(300), distinctUntilChanged()), { initialValue: '' });

  protected readonly users = rxResource({
    params: () => ({ search: this.debouncedSearch(), role: this.role(), page: this.page() + 1, pageSize: 20 }),
    stream: ({ params }) => this.api.list(params),
  });
  private readonly managerList = rxResource({ stream: () => this.api.list({ search: '', role: 'Manager', page: 1, pageSize: 100 }) });
  protected readonly managers = computed(() => (this.managerList.value()?.items ?? []).filter((m) => m.isActive && m.id !== this.editingId()));

  protected readonly open = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly selectedRoles = signal<string[]>([]);
  protected readonly saving = signal(false);

  private readonly passwordPolicy = (control: { value: string }) =>
    !control.value || PASSWORD_RULES.every((r) => r.test(control.value)) ? null : { policy: true };

  protected readonly form = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    managerId: new FormControl<string | null>(null),
    isActive: new FormControl(true, { nonNullable: true }),
    password: new FormControl('', { nonNullable: true, validators: [this.passwordPolicy] }),
  });

  protected initials(name: string): string {
    return name
      .split(' ')
      .filter((p) => p.length > 0)
      .slice(0, 2)
      .map((p) => p[0].toUpperCase())
      .join('');
  }

  protected onPage(event: PageEvent): void {
    this.page.set(event.pageIndex);
  }

  protected startCreate(): void {
    this.editingId.set(null);
    this.form.reset({ firstName: '', lastName: '', email: '', managerId: null, isActive: true, password: '' });
    this.form.controls.email.enable();
    this.form.controls.password.setValidators([Validators.required, this.passwordPolicy]);
    this.form.controls.password.updateValueAndValidity();
    this.selectedRoles.set(['Employee']);
    this.open.set(true);
  }

  protected edit(user: UserSummary): void {
    this.editingId.set(user.id);
    this.open.set(true);
    this.form.controls.password.setValidators([this.passwordPolicy]);
    this.api.get(user.id).subscribe((u) => {
      this.form.reset({ firstName: u.firstName, lastName: u.lastName, email: u.email, managerId: u.managerId, isActive: u.isActive, password: '' });
      this.form.controls.email.disable();
      this.selectedRoles.set([...u.roles]);
    });
  }

  protected toggleRole(role: string, checked: boolean): void {
    this.selectedRoles.update((list) => (checked ? [...new Set([...list, role])] : list.filter((r) => r !== role)));
  }

  protected save(): void {
    const value = this.form.getRawValue();
    const id = this.editingId();
    this.saving.set(true);
    const done = () => {
      this.saving.set(false);
      this.open.set(false);
      this.snackBar.open(this.translate.instant('common.saved') as string, undefined, { duration: 3000 });
      this.users.reload();
      this.managerList.reload();
    };
    const fail = (error: unknown) => {
      this.saving.set(false);
      this.snackBar.open(problemMessage(this.translate, error), this.translate.instant('common.dismiss') as string, { duration: 6000 });
    };

    if (!id) {
      this.api
        .create({ email: value.email, firstName: value.firstName, lastName: value.lastName, roles: this.selectedRoles(), managerId: value.managerId, password: value.password })
        .subscribe({ next: done, error: fail });
      return;
    }

    this.api
      .update(id, { firstName: value.firstName, lastName: value.lastName, roles: this.selectedRoles(), managerId: value.managerId, isActive: value.isActive })
      .subscribe({
        next: () => {
          if (!value.password) {
            done();
            return;
          }
          this.api.resetPassword(id, value.password).subscribe({ next: done, error: fail });
        },
        error: fail,
      });
  }
}
