import { Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

/** Employee portal frame: catalog, "My requests" and help centre tabs. */
@Component({
  selector: 'app-portal-layout',
  imports: [MatTabsModule, RouterLink, RouterLinkActive, RouterOutlet, TranslatePipe],
  template: `
    <nav mat-tab-nav-bar [tabPanel]="panel" class="tabs">
      <a mat-tab-link routerLink="catalog" routerLinkActive #catalog="routerLinkActive" [active]="catalog.isActive">{{ 'portal.newRequestTab' | translate }}</a>
      <a mat-tab-link routerLink="requests" routerLinkActive #requests="routerLinkActive" [active]="requests.isActive">{{ 'portal.myRequestsTab' | translate }}</a>
      <a mat-tab-link routerLink="help" routerLinkActive #help="routerLinkActive" [active]="help.isActive" data-testid="help-tab">{{ 'help.tab' | translate }}</a>
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
export class PortalLayout {}
