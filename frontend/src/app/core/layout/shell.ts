import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AREAS } from '../auth/areas';
import { AuthService } from '../auth/auth.service';

/** Application frame: top bar with user menu, and a side navigation filtered by the user's roles. */
@Component({
  selector: 'app-shell',
  imports: [
    MatButtonModule,
    MatDividerModule,
    MatIconModule,
    MatListModule,
    MatMenuModule,
    MatSidenavModule,
    MatToolbarModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
  ],
  template: `
    <mat-toolbar class="topbar">
      @if (isHandset()) {
        <button mat-icon-button (click)="drawer.toggle()" aria-label="Toggle navigation">
          <mat-icon fontSet="material-symbols-outlined">menu</mat-icon>
        </button>
      }
      <a routerLink="/" class="brand">
        <mat-icon fontSet="material-symbols-outlined">support_agent</mat-icon>
        <span>HR Service Desk</span>
      </a>
      <span class="spacer"></span>
      <span class="tenant">{{ auth.user()?.tenantName }}</span>
      <button mat-button [matMenuTriggerFor]="userMenu" class="user-button" data-testid="user-menu">
        <mat-icon fontSet="material-symbols-outlined">account_circle</mat-icon>
        <span>{{ auth.user()?.fullName }}</span>
      </button>
      <mat-menu #userMenu="matMenu">
        <div class="menu-header" mat-menu-item disabled>
          <span>{{ auth.user()?.email }}</span>
        </div>
        <mat-divider />
        <button mat-menu-item (click)="auth.logout()" data-testid="logout">
          <mat-icon fontSet="material-symbols-outlined">logout</mat-icon>
          <span>Sign out</span>
        </button>
      </mat-menu>
    </mat-toolbar>

    <mat-sidenav-container class="container">
      <mat-sidenav #drawer [mode]="isHandset() ? 'over' : 'side'" [opened]="!isHandset()" class="sidenav">
        <mat-nav-list>
          <a mat-list-item routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
            <mat-icon matListItemIcon fontSet="material-symbols-outlined">home</mat-icon>
            <span matListItemTitle>Home</span>
          </a>
          @for (area of areas(); track area.path) {
            <a mat-list-item [routerLink]="['/', area.path]" routerLinkActive="active" [attr.data-testid]="'nav-' + area.path">
              <mat-icon matListItemIcon fontSet="material-symbols-outlined">{{ area.icon }}</mat-icon>
              <span matListItemTitle>{{ area.label }}</span>
            </a>
          }
        </mat-nav-list>
      </mat-sidenav>
      <mat-sidenav-content>
        <main class="content">
          <router-outlet />
        </main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
    }
    .topbar {
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
      gap: 8px;
      position: relative;
      z-index: 2;
    }
    .brand {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      color: inherit;
      text-decoration: none;
      font: var(--mat-sys-title-large);
    }
    .spacer {
      flex: 1;
    }
    .tenant {
      font: var(--mat-sys-body-medium);
      opacity: 0.85;
    }
    .user-button {
      color: inherit;
    }
    .container {
      flex: 1;
    }
    .sidenav {
      width: 240px;
    }
    .active {
      background: var(--mat-sys-secondary-container);
    }
    .content {
      padding: 24px;
      max-width: 1200px;
      margin: 0 auto;
    }
    @media (max-width: 599px) {
      .tenant,
      .user-button span {
        display: none;
      }
    }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly areas = computed(() => AREAS.filter((area) => this.auth.hasAnyRole(area.roles)));
  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe(Breakpoints.Handset)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );
}
