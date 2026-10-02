import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** Split screen for the sign-in pages: brand panel on the left (hidden on small screens), form on the right. */
@Component({
  selector: 'app-auth-layout',
  imports: [MatIconModule],
  template: `
    <div class="page">
      <aside class="hero" aria-hidden="true">
        <div class="brand">
          <span class="logo"><mat-icon fontSet="material-symbols-outlined">support_agent</mat-icon></span>
          <span>HR Service Desk</span>
        </div>
        <div class="pitch">
          <h2>HR service delivery,<br />on time, every time.</h2>
          <ul>
            <li><mat-icon fontSet="material-symbols-outlined">schedule</mat-icon> Deadlines in business hours, with automatic escalation</li>
            <li><mat-icon fontSet="material-symbols-outlined">fact_check</mat-icon> Approval workflows your managers actually use</li>
            <li><mat-icon fontSet="material-symbols-outlined">lock</mat-icon> Confidential cases seen only by the right people</li>
          </ul>
        </div>
        <p class="footnote">Payroll · Leave · Certificates · Benefits · Training</p>
      </aside>
      <main class="panel">
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
      inset: auto -120px -160px auto;
      width: 420px;
      height: 420px;
      border-radius: 50%;
      background: rgba(255, 255, 255, 0.08);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 12px;
      font: 600 1.1rem Inter, sans-serif;
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
      font: 700 2.4rem/1.15 Inter, sans-serif;
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
      display: grid;
      place-items: center;
      padding: 32px 24px;
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
