import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { STRATEGIES, TeamInfo } from '../../core/api/api.models';
import { TeamsApi } from '../../core/api/approvals.api';
import { problemMessage } from '../../core/http/error.interceptor';
import { EnumLabelPipe } from '../../shared/ui/enum-label';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';


/** HR Admin: teams, their assignment strategy and members. */
@Component({
  selector: 'app-teams-admin',
  imports: [ReactiveFormsModule, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, TranslatePipe, EnumLabelPipe],
  template: `
    <div class="header">
      <h1>{{ 'teams.title' | translate }}</h1>
      @if (!editing()) {
        <button mat-flat-button type="button" (click)="edit(null)" data-testid="new-team">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon> {{ 'teams.new' | translate }}
        </button>
      }
    </div>

    @if (editing()) {
      <mat-card appearance="outlined" class="editor">
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="save()" class="form">
            <mat-form-field appearance="outline">
              <mat-label>{{ 'teams.name' | translate }}</mat-label>
              <input matInput formControlName="name" maxlength="100" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ 'teams.strategy' | translate }}</mat-label>
              <mat-select formControlName="strategy">
                @for (s of strategies; track s) {
                  <mat-option [value]="s">{{ s | enumLabel: 'strategy' }}</mat-option>
                }
              </mat-select>
              <mat-hint>{{ 'strategy.help.' + form.controls.strategy.value | translate }}</mat-hint>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ 'teams.members' | translate }}</mat-label>
              <mat-select formControlName="memberIds" multiple>
                @for (user of staff.value() ?? []; track user.id) {
                  <mat-option [value]="user.id">{{ user.fullName }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-checkbox formControlName="isConfidentialGroup">
              {{ 'teams.restrictedLabel' | translate }}
            </mat-checkbox>
            <div class="actions">
              <button mat-button type="button" (click)="editing.set(false)">{{ 'common.cancel' | translate }}</button>
              <button mat-flat-button type="submit" [disabled]="form.invalid || saving()" data-testid="save-team">{{ 'common.save' | translate }}</button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
    }

    <div class="grid">
      @for (team of teams.value() ?? []; track team.id) {
        <mat-card appearance="outlined" [attr.data-testid]="'team-' + team.name">
          <mat-card-header>
            <mat-card-title>{{ team.name }}</mat-card-title>
            <mat-card-subtitle>
              {{ team.strategy | enumLabel: 'strategy' }}
              @if (team.isConfidentialGroup) {
                · <span class="restricted">{{ 'teams.restricted' | translate }}</span>
              }
            </mat-card-subtitle>
            <button mat-icon-button class="edit" (click)="edit(team)" [attr.aria-label]="'teams.editTeam' | translate: { name: team.name }">
              <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
            </button>
          </mat-card-header>
          <mat-card-content>
            <ul class="members">
              @for (m of team.members; track m.id) {
                <li>{{ m.fullName }} <span class="load">{{ 'teams.activeCount' | translate: { count: m.activeCases } }}</span></li>
              } @empty {
                <li class="load">{{ 'teams.noMembers' | translate }}</li>
              }
            </ul>
            @if (team.requestTypes.length > 0) {
              <p class="types">{{ 'teams.handles' | translate: { types: team.requestTypes.join(', ') } }}</p>
            }
          </mat-card-content>
        </mat-card>
      }
    </div>
  `,
  styles: `
    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    h1 {
      font: var(--mat-sys-headline-small);
    }
    .editor {
      max-width: 640px;
      margin-bottom: 16px;
    }
    .form {
      display: grid;
      gap: 8px;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: 16px;
    }
    .edit {
      margin-left: auto;
    }
    .members {
      list-style: none;
      padding: 0;
      margin: 0;
    }
    .restricted {
      color: var(--mat-sys-error);
    }
    .load,
    .types {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class TeamsAdmin {
  private readonly api = inject(TeamsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  protected readonly strategies = STRATEGIES;
  protected readonly teams = rxResource({ stream: () => this.api.list() });
  protected readonly staff = rxResource({ stream: () => this.api.staff() });
  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  private editedId: string | null = null;

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    strategy: new FormControl<string>('RoundRobin', { nonNullable: true }),
    memberIds: new FormControl<string[]>([], { nonNullable: true }),
    isConfidentialGroup: new FormControl(false, { nonNullable: true }),
  });


  protected edit(team: TeamInfo | null): void {
    this.editedId = team?.id ?? null;
    this.form.setValue({
      name: team?.name ?? '',
      strategy: team?.strategy ?? 'RoundRobin',
      memberIds: team?.members.map((m) => m.id) ?? [],
      isConfidentialGroup: team?.isConfidentialGroup ?? false,
    });
    this.editing.set(true);
  }

  protected save(): void {
    this.saving.set(true);
    this.api.save(this.editedId, this.form.getRawValue()).subscribe({
      next: () => {
        this.saving.set(false);
        this.editing.set(false);
        this.snackBar.open(this.translate.instant('teams.saved'), undefined, { duration: 3000 });
        this.teams.reload();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.snackBar.open(problemMessage(this.translate, error, 'teams.saveFailed'), this.translate.instant('common.dismiss'), { duration: 6000 });
      },
    });
  }
}
