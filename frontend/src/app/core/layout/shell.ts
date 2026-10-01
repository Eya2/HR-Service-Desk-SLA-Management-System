import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterOutlet } from '@angular/router';

/** Application frame: top bar and routed content. Navigation and user menu arrive with auth (phase 2). */
@Component({
  selector: 'app-shell',
  imports: [MatToolbarModule, MatIconModule, RouterLink, RouterOutlet],
  template: `
    <mat-toolbar class="topbar">
      <a routerLink="/" class="brand">
        <mat-icon fontSet="material-symbols-outlined">support_agent</mat-icon>
        <span>HR Service Desk</span>
      </a>
    </mat-toolbar>
    <main class="content">
      <router-outlet />
    </main>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100%;
    }
    .topbar {
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
    }
    .brand {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      color: inherit;
      text-decoration: none;
      font: var(--mat-sys-title-large);
    }
    .content {
      flex: 1;
      padding: 24px;
      max-width: 1200px;
      width: 100%;
      margin: 0 auto;
      box-sizing: border-box;
    }
  `,
})
export class Shell {}
