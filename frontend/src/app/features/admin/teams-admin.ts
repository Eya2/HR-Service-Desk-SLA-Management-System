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
import { problemOf } from '../../core/http/error.interceptor';
import { humanize } from '../../shared/ui/labels';

const STRATEGY_HELP: Record<string, string> = {
  Manual: 'Cases wait in the team queue until someone takes them.',
  RoundRobin: 'Members receive new cases in turn.',
  LeastLoaded: 'The member with the fewest active cases receives the next one.',
};

/** HR Admin: teams, their assignment strategy and members. */
@Component({
  selector: 'app-teams-admin',
  imports: [ReactiveFormsModule, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <div class="header">
      <h1>Teams</h1>
      @if (!editing()) {
        <button mat-flat-button type="button" (click)="edit(null)" data-testid="new-team">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon> New team
        </button>
      }
    </div>

    @if (editing()) {
      <mat-card appearance="outlined" class="editor">
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="save()" class="form">
            <mat-form-field appearance="outline">
              <mat-label>Name</mat-label>
              <input matInput formControlName="name" maxlength="100" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Assignment strategy</mat-label>
              <mat-select formControlName="strategy">
                @for (s of strategies; track s) {
                  <mat-option [value]="s">{{ humanize(s) }}</mat-option>
                }
              </mat-select>
              <mat-hint>{{ help(form.controls.strategy.value) }}</mat-hint>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Members</mat-label>
              <mat-select formControlName="memberIds" multiple>
                @for (user of staff.value() ?? []; track user.id) {
                  <mat-option [value]="user.id">{{ user.fullName }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-checkbox formControlName="isConfidentialGroup">
              Restricted HR group: its members (and the requester) are the only people who see confidential cases
            </mat-checkbox>
            <div class="actions">
              <button mat-button type="button" (click)="editing.set(false)">Cancel</button>
              <button mat-flat-button type="submit" [disabled]="form.invalid || saving()" data-testid="save-team">Save</button>
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
              {{ humanize(team.strategy) }}
              @if (team.isConfidentialGroup) {
                · <span class="restricted">restricted group</span>
              }
            </mat-card-subtitle>
            <button mat-icon-button class="edit" (click)="edit(team)" [attr.aria-label]="'Edit ' + team.name">
              <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
            </button>
          </mat-card-header>
          <mat-card-content>
            <ul class="members">
              @for (m of team.members; track m.id) {
                <li>{{ m.fullName }} <span class="load">{{ m.activeCases }} active</span></li>
              } @empty {
                <li class="load">No members</li>
              }
            </ul>
            @if (team.requestTypes.length > 0) {
              <p class="types">Handles: {{ team.requestTypes.join(', ') }}</p>
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

  protected readonly strategies = STRATEGIES;
  protected readonly humanize = humanize;
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

  protected help(strategy: string): string {
    return STRATEGY_HELP[strategy] ?? '';
  }

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
        this.snackBar.open('Team saved.', undefined, { duration: 3000 });
        this.teams.reload();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.snackBar.open(problemOf(error)?.title ?? 'The team could not be saved.', 'Dismiss', { duration: 6000 });
      },
    });
  }
}
