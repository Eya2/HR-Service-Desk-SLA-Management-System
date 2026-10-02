import { Component, inject } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

/** Approvals area: the queue for every approver, plus the team view for managers. */
@Component({
  selector: 'app-manager-layout',
  imports: [MatTabsModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <nav mat-tab-nav-bar [tabPanel]="panel" class="tabs">
      <a mat-tab-link routerLink="queue" routerLinkActive #queue="routerLinkActive" [active]="queue.isActive">Pending approvals</a>
      @if (isManager) {
        <a mat-tab-link routerLink="team" routerLinkActive #team="routerLinkActive" [active]="team.isActive" data-testid="team-tab">My team</a>
      }
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
export class ManagerLayout {
  protected readonly isManager = inject(AuthService).hasAnyRole(['Manager']);
}
