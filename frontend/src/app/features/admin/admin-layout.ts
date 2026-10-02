import { Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

/** HR Admin area: workflows, teams, SLA, escalation, retention and the help centre. */
@Component({
  selector: 'app-admin-layout',
  imports: [MatTabsModule, RouterLink, RouterLinkActive, RouterOutlet, TranslatePipe],
  template: `
    <nav mat-tab-nav-bar [tabPanel]="panel" class="tabs">
      <a mat-tab-link routerLink="users" routerLinkActive #usr="routerLinkActive" [active]="usr.isActive" data-testid="users-tab">{{ 'users.tab' | translate }}</a>
      <a mat-tab-link routerLink="workflows" routerLinkActive #workflows="routerLinkActive" [active]="workflows.isActive">
        {{ 'admin.workflowsTab' | translate }}
      </a>
      <a mat-tab-link routerLink="teams" routerLinkActive #teams="routerLinkActive" [active]="teams.isActive">{{ 'admin.teamsTab' | translate }}</a>
      <a mat-tab-link routerLink="sla" routerLinkActive #sla="routerLinkActive" [active]="sla.isActive">{{ 'admin.slaTab' | translate }}</a>
      <a mat-tab-link routerLink="escalations" routerLinkActive #esc="routerLinkActive" [active]="esc.isActive">{{ 'admin.escalationTab' | translate }}</a>
      <a mat-tab-link routerLink="retention" routerLinkActive #ret="routerLinkActive" [active]="ret.isActive">{{ 'admin.retentionTab' | translate }}</a>
      <a mat-tab-link routerLink="knowledge" routerLinkActive #kb="routerLinkActive" [active]="kb.isActive" data-testid="kb-tab">{{ 'kbAdmin.tab' | translate }}</a>
      <a mat-tab-link routerLink="integrations" routerLinkActive #integ="routerLinkActive" [active]="integ.isActive" data-testid="integrations-tab">{{ 'integrations.tab' | translate }}</a>
    </nav>
    <mat-tab-nav-panel #panel>
      <router-outlet />
    </mat-tab-nav-panel>
  `,
  styles: `
    .tabs {
      margin-bottom: 24px;
    }
  `,
})
export class AdminLayout {}
