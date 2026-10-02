import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageMenu } from '../../core/i18n/language-menu';

/** Split screen for the sign-in pages: brand panel on the left (hidden on small screens), form on the right. */
@Component({
  selector: 'app-auth-layout',
  imports: [MatIconModule, TranslatePipe, LanguageMenu],
  template: `
    <div class="page">
      <aside class="hero" aria-hidden="true">
        <div class="brand">
          <span class="logo"><mat-icon fontSet="material-symbols-outlined">support_agent</mat-icon></span>
          <span>{{ 'common.appName' | translate }}</span>
        </div>
        <div class="pitch">
          <h2 [innerHTML]="'auth.pitchTitle' | translate"></h2>
          <ul>
            <li><mat-icon fontSet="material-symbols-outlined">schedule</mat-icon> {{ 'auth.pitch1' | translate }}</li>
            <li><mat-icon fontSet="material-symbols-outlined">fact_check</mat-icon> {{ 'auth.pitch2' | translate }}</li>
            <li><mat-icon fontSet="material-symbols-outlined">lock</mat-icon> {{ 'auth.pitch3' | translate }}</li>
          </ul>
        </div>
        <p class="footnote">{{ 'auth.footnote' | translate }}</p>
      </aside>
      <main class="panel">
        <div class="lang"><app-language-menu /></div>
        <div class="form-wrap">
          <ng-content />
        </div>
      </main>
    </div>
  `,
  styles: `
    .page {
      min-height: 100vh;
      display: grid;
      grid-template-columns: minmax(0, 1.1fr) minmax(0, 1fr);
      background: var(--app-page-bg);
    }
    .hero {
      position: relative;
      overflow: hidden;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      padding: 48px;
      color: #fff;
      background: var(--app-brand-gradient);
    }
    .hero::after {
      content: '';
      position: absolute;
      inset-block-end: -160px;
      inset-inline-end: -120px;
      width: 420px;
      height: 420px;
      border-radius: 50%;
      background: rgba(255, 255, 255, 0.08);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 12px;
      font: 600 1.1rem Inter, 'IBM Plex Sans Arabic', sans-serif;
    }
    .logo {
      width: 40px;
      height: 40px;
      border-radius: 12px;
      display: grid;
      place-items: center;
      background: rgba(255, 255, 255, 0.18);
    }
    .pitch h2 {
      font: 700 2.4rem/1.15 Inter, 'IBM Plex Sans Arabic', sans-serif;
      letter-spacing: -0.03em;
      margin: 0 0 28px;
    }
    .pitch ul {
      list-style: none;
      padding: 0;
      margin: 0;
      display: grid;
      gap: 14px;
      font-size: 1rem;
    }
    .pitch li {
      display: flex;
      align-items: center;
      gap: 12px;
      opacity: 0.95;
    }
    .footnote {
      opacity: 0.75;
      margin: 0;
    }
    .panel {
      position: relative;
      display: grid;
      place-items: center;
      padding: 32px 24px;
    }
    .lang {
      position: absolute;
      top: 16px;
      inset-inline-end: 16px;
    }
    .form-wrap {
      width: 100%;
      max-width: 400px;
    }
    @media (max-width: 899px) {
      .page {
        grid-template-columns: 1fr;
      }
      .hero {
        display: none;
      }
    }
  `,
})
export class AuthLayout {}
