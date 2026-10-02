import { Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/** HR Admin area. More sections (users, catalog, SLA policies) are added by later phases. */
@Component({
  selector: 'app-admin-layout',
  imports: [MatTabsModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <nav mat-tab-nav-bar [tabPanel]="panel" class="tabs">
      <a mat-tab-link routerLink="workflows" routerLinkActive #workflows="routerLinkActive" [active]="workflows.isActive">
        Approval workflows
      </a>
      <a mat-tab-link routerLink="teams" routerLinkActive #teams="routerLinkActive" [active]="teams.isActive">Teams</a>
      <a mat-tab-link routerLink="sla" routerLinkActive #sla="routerLinkActive" [active]="sla.isActive">SLA & calendar</a>
      <a mat-tab-link routerLink="escalations" routerLinkActive #esc="routerLinkActive" [active]="esc.isActive">Escalation</a>
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
