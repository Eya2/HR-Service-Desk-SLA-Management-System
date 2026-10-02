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
import { problemMessage, problemOf } from '../../core/http/error.interceptor';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

const TRIGGERS: EscalationRuleInfo['trigger'][] = ['AtRisk', 'Breached', 'NoResponseFor'];

const ACTIONS: EscalationRuleInfo['action'][] = ['NotifyAssignee', 'NotifyManager', 'BumpPriority', 'ReassignToTeam'];

/** HR Admin: escalation rules ("when …, do …"). Each rule fires at most once per case. */
@Component({
  selector: 'app-escalations-admin',
  imports: [ReactiveFormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSlideToggleModule, TranslatePipe],
  template: `
    <h1>{{ 'escalation.title' | translate }}</h1>
    <p class="intro">{{ 'escalation.intro' | translate }}</p>

    <div class="rules">
      @for (rule of rules.value() ?? []; track rule.id) {
        <mat-card appearance="outlined" [attr.data-testid]="'rule-' + rule.name">
          <mat-card-content class="rule">
            <div>
              <strong>{{ rule.name }}</strong>
              <p>{{ 'escalation.rule' | translate: { trigger: describe(rule), action: ('escalation.action.' + rule.action) | translate, team: teamSuffix(rule) } }}</p>
              <p class="fired">{{ 'escalation.fired' | translate: { count: rule.timesFired } }}</p>
            </div>
            <mat-slide-toggle [checked]="rule.isActive" (change)="toggle(rule, $event.checked)" [attr.aria-label]="'escalation.activeAria' | translate: { name: rule.name }" />
          </mat-card-content>
        </mat-card>
      }
    </div>

    <mat-card appearance="outlined" class="new">
      <mat-card-header><mat-card-title>{{ 'escalation.newRule' | translate }}</mat-card-title></mat-card-header>
      <mat-card-content>
        <form [formGroup]="form" (ngSubmit)="create()" class="form">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'escalation.name' | translate }}</mat-label>
            <input matInput formControlName="name" maxlength="100" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'escalation.when' | translate }}</mat-label>
            <mat-select formControlName="trigger">
              @for (t of triggerKeys; track t) {
                <mat-option [value]="t">{{ 'escalation.trigger.' + t | translate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (form.controls.trigger.value === 'NoResponseFor') {
            <mat-form-field appearance="outline">
              <mat-label>{{ 'escalation.hoursWithout' | translate }}</mat-label>
              <input matInput type="number" min="1" max="720" formControlName="noResponseHours" />
            </mat-form-field>
          }
          <mat-form-field appearance="outline">
            <mat-label>{{ 'escalation.do' | translate }}</mat-label>
            <mat-select formControlName="action">
              @for (a of actionKeys; track a) {
                <mat-option [value]="a">{{ 'escalation.action.' + a | translate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (form.controls.action.value === 'ReassignToTeam') {
            <mat-form-field appearance="outline">
              <mat-label>{{ 'escalation.team' | translate }}</mat-label>
              <mat-select formControlName="targetTeamId">
                @for (team of teams.value() ?? []; track team.id) {
                  <mat-option [value]="team.id">{{ team.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          }
          <div class="actions">
            <button mat-flat-button type="submit" [disabled]="form.invalid" data-testid="create-rule">{{ 'escalation.add' | translate }}</button>
          </div>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
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

  private readonly translate = inject(TranslateService);
  protected readonly triggerKeys = TRIGGERS;
  protected readonly actionKeys = ACTIONS;
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
    return rule.trigger === 'NoResponseFor'
      ? this.translate.instant('escalation.noResponseFor', { hours: rule.noResponseHours })
      : (this.translate.instant(`escalation.trigger.${rule.trigger}`) as string).toLowerCase();
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
        this.snackBar.open(problemOf(error)?.detail ?? problemMessage(this.translate, error, 'escalation.saveFailed'), this.translate.instant('common.dismiss'), {
          duration: 6000,
        });
        this.rules.reload();
      },
    });
  }
}
