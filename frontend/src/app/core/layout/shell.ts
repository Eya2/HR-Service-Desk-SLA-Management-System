import { BreakpointObserver } from '@angular/cdk/layout';
import { DatePipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AREAS } from '../auth/areas';
import { AuthService } from '../auth/auth.service';
import { AppNotification, NotificationService } from '../notifications/notification.service';
import { ThemeService } from './theme.service';

/** Application frame: a sidebar with the brand and the user's areas, and a light top bar. */
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
    MatTooltipModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
  ],
  template: `
    <mat-sidenav-container class="container">
      <mat-sidenav #drawer [mode]="compact() ? 'over' : 'side'" [opened]="!compact()" class="sidenav">
        <a routerLink="/" class="brand" (click)="compact() && drawer.close()">
          <span class="logo"><mat-icon fontSet="material-symbols-outlined">support_agent</mat-icon></span>
          <span class="brand-text">
            <strong>HR Service Desk</strong>
            <small>{{ auth.user()?.tenantName }}</small>
          </span>
        </a>

        <nav class="nav" aria-label="Main">
          <span class="nav-label">Workspace</span>
          <a class="nav-item" routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" (click)="compact() && drawer.close()">
            <mat-icon fontSet="material-symbols-outlined">home</mat-icon><span>Home</span>
          </a>
          @for (area of areas(); track area.path) {
            <a
              class="nav-item"
              [routerLink]="['/', area.path]"
              routerLinkActive="active"
              [attr.data-testid]="'nav-' + area.path"
              (click)="compact() && drawer.close()"
            >
              <mat-icon fontSet="material-symbols-outlined">{{ area.icon }}</mat-icon><span>{{ area.label }}</span>
            </a>
          }
        </nav>

        <div class="sidebar-footer">
          <span class="avatar small" aria-hidden="true">{{ initials() }}</span>
          <span class="who">
            <strong>{{ auth.user()?.fullName }}</strong>
            <small>{{ roleLabel() }}</small>
          </span>
        </div>
      </mat-sidenav>

      <mat-sidenav-content class="content-area">
        <header class="topbar">
          @if (compact()) {
            <button mat-icon-button (click)="drawer.toggle()" aria-label="Toggle navigation">
              <mat-icon fontSet="material-symbols-outlined">menu</mat-icon>
            </button>
          }
          <span class="spacer"></span>

          <button mat-icon-button (click)="theme.cycle()" [matTooltip]="themeTooltip()" [attr.aria-label]="themeTooltip()" data-testid="theme-toggle">
            <mat-icon fontSet="material-symbols-outlined">{{ themeIcon() }}</mat-icon>
          </button>

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

          <button class="user-button" [matMenuTriggerFor]="userMenu" data-testid="user-menu" [attr.aria-label]="'Account: ' + auth.user()?.fullName">
            <span class="avatar" aria-hidden="true">{{ initials() }}</span>
          </button>
          <mat-menu #userMenu="matMenu" xPosition="before">
            <div class="menu-header" (click)="$event.stopPropagation()">
              <strong>{{ auth.user()?.fullName }}</strong>
              <small>{{ auth.user()?.email }}</small>
            </div>
            <mat-divider />
            <button mat-menu-item (click)="auth.logout()" data-testid="logout">
              <mat-icon fontSet="material-symbols-outlined">logout</mat-icon>
              <span>Sign out</span>
            </button>
          </mat-menu>
        </header>

        <main class="content">
          <router-outlet />
        </main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    :host {
      display: block;
      height: 100%;
    }
    .container {
      height: 100%;
      background: var(--app-page-bg);
    }
    .sidenav {
      width: 264px;
      border-right: 1px solid var(--app-border);
      background: var(--app-card-bg);
      display: flex;
      flex-direction: column;
    }
    .sidenav ::ng-deep .mat-drawer-inner-container {
      display: flex;
      flex-direction: column;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 20px 20px 16px;
      color: inherit;
      text-decoration: none;
    }
    .logo {
      width: 40px;
      height: 40px;
      border-radius: 12px;
      display: grid;
      place-items: center;
      background: var(--app-brand-gradient);
      color: #fff;
      box-shadow: 0 6px 16px rgba(79, 70, 229, 0.35);
    }
    .brand-text {
      display: grid;
      line-height: 1.2;
    }
    .brand-text small,
    .who small {
      color: var(--app-muted);
      font-size: 0.78rem;
    }
    .nav {
      display: flex;
      flex-direction: column;
      gap: 2px;
      padding: 8px 12px;
      flex: 1;
    }
    .nav-label {
      font-size: 0.7rem;
      font-weight: 600;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--app-muted);
      padding: 12px 12px 6px;
    }
    .nav-item {
      position: relative;
      display: flex;
      align-items: center;
      gap: 12px;
      height: 40px;
      padding: 0 12px;
      border-radius: 10px;
      color: var(--app-muted);
      text-decoration: none;
      font-weight: 500;
      font-size: 0.92rem;
      transition: background 120ms, color 120ms;
    }
    .nav-item mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
    }
    .nav-item:hover {
      background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent);
      color: var(--mat-sys-on-surface);
    }
    .nav-item.active {
      background: color-mix(in srgb, var(--mat-sys-primary) 12%, transparent);
      color: var(--mat-sys-primary);
      font-weight: 600;
    }
    .nav-item.active::before {
      content: '';
      position: absolute;
      left: -12px;
      top: 8px;
      bottom: 8px;
      width: 3px;
      border-radius: 0 3px 3px 0;
      background: var(--mat-sys-primary);
    }
    .sidebar-footer {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 16px 20px;
      border-top: 1px solid var(--app-border);
    }
    .who {
      display: grid;
      line-height: 1.2;
      min-width: 0;
    }
    .who strong {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .avatar {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      display: grid;
      place-items: center;
      font: 600 0.8rem/1 Inter, sans-serif;
      background: var(--app-brand-gradient);
      color: #fff;
    }
    .avatar.small {
      width: 32px;
      height: 32px;
      flex: none;
    }
    .content-area {
      display: flex;
      flex-direction: column;
    }
    .topbar {
      position: sticky;
      top: 0;
      z-index: 2;
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 10px 24px;
      background: color-mix(in srgb, var(--app-page-bg) 85%, transparent);
      backdrop-filter: blur(8px);
    }
    .spacer {
      flex: 1;
    }
    .user-button {
      border: 0;
      padding: 0;
      margin-left: 8px;
      background: none;
      cursor: pointer;
      border-radius: 50%;
    }
    .user-button:focus-visible {
      outline: 2px solid var(--mat-sys-primary);
      outline-offset: 2px;
    }
    .content {
      padding: 8px 32px 40px;
      max-width: 1280px;
      width: 100%;
      margin: 0 auto;
      box-sizing: border-box;
    }
    .menu-title,
    .menu-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 8px 16px;
      font-weight: 600;
    }
    .menu-header {
      display: grid;
      gap: 2px;
    }
    .menu-header small {
      color: var(--app-muted);
      font-weight: 400;
    }
    .unread .n-title {
      font-weight: 600;
    }
    .n-title {
      display: block;
    }
    .n-meta {
      display: block;
      font-size: 0.78rem;
      color: var(--app-muted);
    }
    .empty-menu {
      padding: 8px 16px;
      color: var(--app-muted);
    }
    @media (max-width: 599px) {
      .content {
        padding: 8px 16px 32px;
      }
    }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly notifications = inject(NotificationService);
  protected readonly theme = inject(ThemeService);
  private readonly router = inject(Router);

  protected readonly areas = computed(() => AREAS.filter((area) => this.auth.hasAnyRole(area.roles)));
  protected readonly compact = toSignal(
    inject(BreakpointObserver)
      .observe('(max-width: 959px)')
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly initials = computed(() => {
    const user = this.auth.user();
    return user ? `${user.firstName[0] ?? ''}${user.lastName[0] ?? ''}`.toUpperCase() : '';
  });

  /** The most senior role, for the sidebar footer. */
  protected readonly roleLabel = computed(() => {
    const order = ['SuperAdmin', 'HrAdmin', 'Auditor', 'PayrollSpecialist', 'HrOfficer', 'Manager', 'Employee'];
    const role = order.find((r) => this.auth.user()?.roles.includes(r as never)) ?? '';
    return role.replace(/([a-z])([A-Z])/g, '$1 $2');
  });

  protected readonly themeIcon = computed(() => ({ light: 'light_mode', dark: 'dark_mode', system: 'contrast' })[this.theme.mode()]);
  protected readonly themeTooltip = computed(() => `Theme: ${this.theme.mode()} (click to change)`);

  protected open(notification: AppNotification): void {
    this.notifications.markRead(notification);
    if (notification.ticketId) void this.router.navigate(['/tickets', notification.ticketId]);
  }
}
