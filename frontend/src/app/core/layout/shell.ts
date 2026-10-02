import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { DatePipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AREAS } from '../auth/areas';
import { AuthService } from '../auth/auth.service';
import { AppNotification, NotificationService } from '../notifications/notification.service';

/** Application frame: top bar with user menu, and a side navigation filtered by the user's roles. */
@Component({
  selector: 'app-shell',
  imports: [
    DatePipe,
    MatBadgeModule,
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
      <button
        mat-icon-button
        [matMenuTriggerFor]="notificationsMenu"
        (menuOpened)="notifications.refresh()"
        [attr.aria-label]="'Notifications: ' + notifications.unreadCount() + ' unread'"
        data-testid="bell"
      >
        <mat-icon
          fontSet="material-symbols-outlined"
          [matBadge]="notifications.unreadCount()"
          [matBadgeHidden]="!notifications.hasUnread()"
          matBadgeColor="warn"
          matBadgeSize="small"
          >notifications</mat-icon
        >
      </button>
      <mat-menu #notificationsMenu="matMenu" class="notifications-menu">
        <div class="menu-title" (click)="$event.stopPropagation()">
          <span>Notifications</span>
          @if (notifications.hasUnread()) {
            <button mat-button (click)="notifications.markAllRead()">Mark all read</button>
          }
        </div>
        @for (n of notifications.latest(); track n.id) {
          <button mat-menu-item (click)="open(n)" [class.unread]="!n.isRead" data-testid="notification">
            <span class="n-title">{{ n.title }}</span>
            <span class="n-meta">{{ n.createdAt | date: 'short' }}</span>
          </button>
        } @empty {
          <p class="empty-menu">You are all caught up.</p>
        }
      </mat-menu>
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
    .menu-title {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 4px 16px;
      font: var(--mat-sys-title-small);
    }
    .unread .n-title {
      font-weight: 600;
    }
    .n-title {
      display: block;
    }
    .n-meta {
      display: block;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .empty-menu {
      padding: 8px 16px;
      color: var(--mat-sys-on-surface-variant);
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
  protected readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  protected open(notification: AppNotification): void {
    this.notifications.markRead(notification);
    if (notification.ticketId) void this.router.navigate(['/tickets', notification.ticketId]);
  }
  protected readonly areas = computed(() => AREAS.filter((area) => this.auth.hasAnyRole(area.roles)));
  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe(Breakpoints.Handset)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );
}
