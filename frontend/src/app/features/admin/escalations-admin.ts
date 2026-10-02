import { Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EscalationRuleInfo } from '../../core/api/api.models';
import { EscalationsApi, TeamsApi } from '../../core/api/approvals.api';
import { problemOf } from '../../core/http/error.interceptor';

const TRIGGERS: Record<EscalationRuleInfo['trigger'], string> = {
  AtRisk: 'SLA at risk',
  Breached: 'SLA breached',
  NoResponseFor: 'No response for…',
};

const ACTIONS: Record<EscalationRuleInfo['action'], string> = {
  NotifyAssignee: 'Notify the assignee',
  NotifyManager: "Notify the assignee's manager",
  BumpPriority: 'Raise the priority',
  ReassignToTeam: 'Reassign to a team',
};

/** HR Admin: escalation rules ("when …, do …"). Each rule fires at most once per case. */
@Component({
  selector: 'app-escalations-admin',
  imports: [ReactiveFormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSlideToggleModule],
  template: `
    <h1>Escalation rules</h1>
    <p class="intro">The SLA monitor checks running cases every minute. Each rule fires at most once per case.</p>

    <div class="rules">
      @for (rule of rules.value() ?? []; track rule.id) {
        <mat-card appearance="outlined" [attr.data-testid]="'rule-' + rule.name">
          <mat-card-content class="rule">
            <div>
              <strong>{{ rule.name }}</strong>
              <p>When {{ describe(rule) }} → {{ actions[rule.action] }}{{ teamSuffix(rule) }}</p>
              <p class="fired">Fired {{ rule.timesFired }} time(s)</p>
            </div>
            <mat-slide-toggle [checked]="rule.isActive" (change)="toggle(rule, $event.checked)" [attr.aria-label]="'Active: ' + rule.name" />
          </mat-card-content>
        </mat-card>
      }
    </div>

    <mat-card appearance="outlined" class="new">
      <mat-card-header><mat-card-title>New rule</mat-card-title></mat-card-header>
      <mat-card-content>
        <form [formGroup]="form" (ngSubmit)="create()" class="form">
          <mat-form-field appearance="outline">
            <mat-label>Name</mat-label>
            <input matInput formControlName="name" maxlength="100" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>When</mat-label>
            <mat-select formControlName="trigger">
              @for (t of triggerKeys; track t) {
                <mat-option [value]="t">{{ triggers[t] }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (form.controls.trigger.value === 'NoResponseFor') {
            <mat-form-field appearance="outline">
              <mat-label>Business hours without response</mat-label>
              <input matInput type="number" min="1" max="720" formControlName="noResponseHours" />
            </mat-form-field>
          }
          <mat-form-field appearance="outline">
            <mat-label>Do</mat-label>
            <mat-select formControlName="action">
              @for (a of actionKeys; track a) {
                <mat-option [value]="a">{{ actions[a] }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (form.controls.action.value === 'ReassignToTeam') {
            <mat-form-field appearance="outline">
              <mat-label>Team</mat-label>
              <mat-select formControlName="targetTeamId">
                @for (team of teams.value() ?? []; track team.id) {
                  <mat-option [value]="team.id">{{ team.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          }
          <div class="actions">
            <button mat-flat-button type="submit" [disabled]="form.invalid" data-testid="create-rule">Add rule</button>
          </div>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
    .intro,
    .fired {
      color: var(--mat-sys-on-surface-variant);
    }
    .rules {
      display: grid;
      gap: 12px;
      margin-bottom: 24px;
    }
    .rule {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 16px;
    }
    .rule p {
      margin: 4px 0 0;
    }
    .new {
      max-width: 640px;
    }
    .form {
      display: grid;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
    }
  `,
})
export class EscalationsAdmin {
  private readonly api = inject(EscalationsApi);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly triggers = TRIGGERS;
  protected readonly actions = ACTIONS;
  protected readonly triggerKeys = Object.keys(TRIGGERS) as EscalationRuleInfo['trigger'][];
  protected readonly actionKeys = Object.keys(ACTIONS) as EscalationRuleInfo['action'][];
  protected readonly rules = rxResource({ stream: () => this.api.list() });
  protected readonly teams = rxResource({ stream: () => inject(TeamsApi).list() });

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    trigger: new FormControl<EscalationRuleInfo['trigger']>('Breached', { nonNullable: true }),
    noResponseHours: new FormControl<number | null>(4),
    action: new FormControl<EscalationRuleInfo['action']>('NotifyManager', { nonNullable: true }),
    targetTeamId: new FormControl<string | null>(null),
  });

  protected describe(rule: EscalationRuleInfo): string {
    return rule.trigger === 'NoResponseFor' ? `no response for ${rule.noResponseHours} business hours` : TRIGGERS[rule.trigger].toLowerCase();
  }

  protected teamSuffix(rule: EscalationRuleInfo): string {
    const team = this.teams.value()?.find((t) => t.id === rule.targetTeamId);
    return team ? ` (${team.name})` : '';
  }

  protected toggle(rule: EscalationRuleInfo, isActive: boolean): void {
    this.persist({ ...rule, isActive });
  }

  protected create(): void {
    const v = this.form.getRawValue();
    this.persist(
      {
        id: null,
        name: v.name,
        trigger: v.trigger,
        noResponseHours: v.trigger === 'NoResponseFor' ? v.noResponseHours : null,
        action: v.action,
        targetTeamId: v.action === 'ReassignToTeam' ? v.targetTeamId : null,
        requestTypeId: null,
        isActive: true,
      },
      () => this.form.reset(),
    );
  }

  private persist(rule: Omit<EscalationRuleInfo, 'id' | 'timesFired'> & { id: string | null }, after?: () => void): void {
    this.api.save(rule).subscribe({
      next: () => {
        after?.();
        this.rules.reload();
      },
      error: (error: unknown) => {
        this.snackBar.open(problemOf(error)?.detail ?? problemOf(error)?.title ?? 'The rule could not be saved.', 'Dismiss', { duration: 6000 });
        this.rules.reload();
      },
    });
  }
}
