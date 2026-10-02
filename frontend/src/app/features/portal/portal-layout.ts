import { Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/** Employee portal frame: catalog and "My requests" tabs. */
@Component({
  selector: 'app-portal-layout',
  imports: [MatTabsModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <nav mat-tab-nav-bar [tabPanel]="panel" class="tabs">
      <a mat-tab-link routerLink="catalog" routerLinkActive #catalog="routerLinkActive" [active]="catalog.isActive">New request</a>
      <a mat-tab-link routerLink="requests" routerLinkActive #requests="routerLinkActive" [active]="requests.isActive">My requests</a>
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
