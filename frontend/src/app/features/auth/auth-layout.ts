import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageMenu } from '../../core/i18n/language-menu';

/**
 * Sign-in pages: an inset brand panel with a glimpse of the product on the left (hidden on small screens),
 * the form on the right.
 */
@Component({
  selector: 'app-auth-layout',
  imports: [MatIconModule, TranslatePipe, LanguageMenu],
  template: `
    <div class="page">
      <aside class="hero" aria-hidden="true">
        <div class="glow g1"></div>
        <div class="glow g2"></div>
        <div class="brand">
          <span class="logo"><mat-icon fontSet="material-symbols-outlined">support_agent</mat-icon></span>
          <span>{{ 'common.appName' | translate }}</span>
        </div>

        <div class="pitch">
          <h2 [innerHTML]="'auth.pitchTitle' | translate"></h2>
          <ul>
            <li><span class="chip"><mat-icon fontSet="material-symbols-outlined">schedule</mat-icon></span>{{ 'auth.pitch1' | translate }}</li>
            <li><span class="chip"><mat-icon fontSet="material-symbols-outlined">fact_check</mat-icon></span>{{ 'auth.pitch2' | translate }}</li>
            <li><span class="chip"><mat-icon fontSet="material-symbols-outlined">lock</mat-icon></span>{{ 'auth.pitch3' | translate }}</li>
          </ul>
        </div>

        <div class="preview">
          <div class="glass case">
            <div class="row">
              <span class="ref">HR-2026-000123</span>
              <span class="pill ok"><mat-icon fontSet="material-symbols-outlined">task_alt</mat-icon>{{ 'status.Resolved' | translate }}</span>
            </div>
            <strong>{{ 'auth.preview.case' | translate }}</strong>
            <span class="muted">{{ 'auth.preview.early' | translate }}</span>
          </div>
          <div class="glass stat">
            <span class="muted">{{ 'dashboard.compliance' | translate }}</span>
            <strong class="big">94%</strong>
            <span class="bar"><span style="width: 94%"></span></span>
          </div>
          <div class="glass stat">
            <span class="muted">{{ 'dashboard.csat' | translate }}</span>
            <strong class="big">4.6 <mat-icon class="star" fontSet="material-symbols-outlined">star</mat-icon></strong>
            <span class="muted small">{{ 'auth.preview.ratings' | translate }}</span>
          </div>
        </div>

        <p class="footnote">{{ 'auth.footnote' | translate }}</p>
      </aside>

      <main class="panel">
        <div class="lang"><app-language-menu /></div>
        <div class="form-wrap">
          <div class="mobile-brand">
            <span class="logo"><mat-icon fontSet="material-symbols-outlined">support_agent</mat-icon></span>
            <strong>{{ 'common.appName' | translate }}</strong>
          </div>
          <ng-content />
        </div>
      </main>
    </div>
  `,
  styles: `
    .page {
      min-height: 100vh;
      display: grid;
      grid-template-columns: minmax(0, 1.05fr) minmax(0, 1fr);
      background: var(--app-page-bg);
    }
    .hero {
      position: relative;
      overflow: hidden;
      display: flex;
      flex-direction: column;
      gap: 32px;
      margin: 16px;
      padding: 40px 44px;
      border-radius: 28px;
      color: #fff;
      background:
        radial-gradient(120% 80% at 0% 0%, #3b5bfd 0%, transparent 55%),
        radial-gradient(90% 70% at 100% 100%, #8b3dff 0%, transparent 60%),
        linear-gradient(160deg, #1e2a78 0%, #2a1a6e 100%);
    }
    .glow {
      position: absolute;
      border-radius: 50%;
      filter: blur(60px);
      opacity: 0.45;
      pointer-events: none;
    }
    .g1 {
      width: 320px;
      height: 320px;
      top: -80px;
      inset-inline-end: -60px;
      background: #60a5fa;
    }
    .g2 {
      width: 280px;
      height: 280px;
      bottom: -60px;
      inset-inline-start: 20%;
      background: #c084fc;
    }
    .brand,
    .pitch,
    .preview,
    .footnote {
      position: relative;
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
      color: #fff;
      background: rgba(255, 255, 255, 0.16);
      border: 1px solid rgba(255, 255, 255, 0.22);
    }
    .pitch {
      margin-top: auto;
    }
    .pitch h2 {
      font: 700 2.6rem/1.1 Inter, 'IBM Plex Sans Arabic', sans-serif;
      letter-spacing: -0.035em;
      margin: 0 0 24px;
    }
    .pitch ul {
      list-style: none;
      padding: 0;
      margin: 0;
      display: grid;
      gap: 12px;
      font-size: 0.98rem;
    }
    .pitch li {
      display: flex;
      align-items: center;
      gap: 12px;
      color: rgba(255, 255, 255, 0.9);
    }
    .chip {
      flex: none;
      width: 32px;
      height: 32px;
      border-radius: 10px;
      display: grid;
      place-items: center;
      background: rgba(255, 255, 255, 0.12);
      border: 1px solid rgba(255, 255, 255, 0.18);
    }
    .chip mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .preview {
      display: grid;
      grid-template-columns: 1.4fr 1fr 1fr;
      gap: 12px;
      margin-bottom: auto;
    }
    .glass {
      display: grid;
      gap: 6px;
      align-content: start;
      padding: 14px 16px;
      border-radius: 16px;
      background: rgba(255, 255, 255, 0.1);
      border: 1px solid rgba(255, 255, 255, 0.18);
      backdrop-filter: blur(12px);
      box-shadow: 0 12px 30px -12px rgba(0, 0, 0, 0.45);
    }
    .row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .ref {
      font-size: 0.75rem;
      opacity: 0.75;
      letter-spacing: 0.02em;
    }
    .pill {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 8px;
      border-radius: 999px;
      font-size: 0.72rem;
      font-weight: 600;
    }
    .pill mat-icon {
      font-size: 14px;
      width: 14px;
      height: 14px;
    }
    .pill.ok {
      background: rgba(74, 222, 128, 0.2);
      color: #bbf7d0;
    }
    .glass strong {
      font-size: 0.95rem;
    }
    .muted {
      font-size: 0.78rem;
      color: rgba(255, 255, 255, 0.72);
    }
    .small {
      font-size: 0.72rem;
    }
    .big {
      display: flex;
      align-items: center;
      gap: 4px;
      font: 700 1.6rem/1 Inter, 'IBM Plex Sans Arabic', sans-serif !important;
      letter-spacing: -0.02em;
    }
    .star {
      color: #fbbf24;
      font-variation-settings: 'FILL' 1;
    }
    .bar {
      height: 6px;
      border-radius: 6px;
      background: rgba(255, 255, 255, 0.18);
      overflow: hidden;
    }
    .bar span {
      display: block;
      height: 100%;
      border-radius: 6px;
      background: linear-gradient(90deg, #4ade80, #22d3ee);
    }
    .footnote {
      margin: 0;
      font-size: 0.85rem;
      color: rgba(255, 255, 255, 0.6);
    }
    .panel {
      position: relative;
      display: grid;
      place-items: center;
      padding: 40px 24px;
    }
    .lang {
      position: absolute;
      top: 20px;
      inset-inline-end: 20px;
    }
    .form-wrap {
      width: 100%;
      max-width: 400px;
    }
    .mobile-brand {
      display: none;
      align-items: center;
      gap: 10px;
      margin-bottom: 32px;
    }
    .mobile-brand .logo {
      background: var(--app-brand-gradient);
      border: 0;
    }
    @media (max-width: 1099px) {
      .preview {
        grid-template-columns: 1fr 1fr;
      }
      .preview .case {
        grid-column: 1 / -1;
      }
    }
    @media (max-width: 899px) {
      .page {
        grid-template-columns: 1fr;
      }
      .hero {
        display: none;
      }
      .mobile-brand {
        display: flex;
      }
    }
  `,
})
export class AuthLayout {}
