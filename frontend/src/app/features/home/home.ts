import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { AREAS } from '../../core/auth/areas';
import { AuthService } from '../../core/auth/auth.service';

/** Accent per area: a soft tinted chip behind each area's icon. */
const AREA_TINT: Record<string, string> = {
  portal: '#2563eb',
  manager: '#7c3aed',
  agent: '#0891b2',
  dashboard: '#ea580c',
  audit: '#475569',
  admin: '#16a34a',
};

/** Landing page: a greeting banner, quick actions and the areas the user's roles open. */
@Component({
  selector: 'app-home',
  imports: [MatButtonModule, MatIconModule, RouterLink],
  template: `
    <section class="hero">
      <div class="hero-text">
        <p class="eyebrow">{{ auth.user()?.tenantName }}</p>
        <h1>{{ greeting() }}, {{ auth.user()?.firstName }}</h1>
        <p>What would you like to do today?</p>
        <div class="actions">
          @if (has('portal')) {
            <a mat-flat-button routerLink="/portal/catalog" class="primary-action">
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon> New request
            </a>
            <a mat-stroked-button routerLink="/portal/requests" class="ghost">My requests</a>
          }
          @if (has('agent')) {
            <a mat-stroked-button routerLink="/agent" class="ghost">Open the HR queue</a>
          }
        </div>
      </div>
      <div class="hero-art" aria-hidden="true">
        <span class="bubble b1"><mat-icon fontSet="material-symbols-outlined">description</mat-icon></span>
        <span class="bubble b2"><mat-icon fontSet="material-symbols-outlined">verified</mat-icon></span>
        <span class="bubble b3"><mat-icon fontSet="material-symbols-outlined">schedule</mat-icon></span>
      </div>
    </section>

    <h2 class="section-title">Your workspace</h2>
    @if (areas().length > 0) {
      <div class="grid">
        @for (area of areas(); track area.path) {
          <a class="tile" [routerLink]="['/', area.path]" [attr.data-testid]="'area-' + area.path" [style.--tint]="tint(area.path)">
            <span class="chip"><mat-icon fontSet="material-symbols-outlined">{{ area.icon }}</mat-icon></span>
            <span class="tile-text">
              <strong>{{ area.label }}</strong>
              <span>{{ area.description }}</span>
            </span>
            <mat-icon class="arrow" fontSet="material-symbols-outlined">arrow_forward</mat-icon>
          </a>
        }
      </div>
    } @else {
      <p>No work area is available for your role yet.</p>
    }
  `,
  styles: `
    .hero {
      position: relative;
      overflow: hidden;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 24px;
      padding: 32px 36px;
      border-radius: 20px;
      color: #fff;
      background: var(--app-brand-gradient);
      box-shadow: 0 12px 32px rgba(79, 70, 229, 0.25);
    }
    .hero::before {
      content: '';
      position: absolute;
      inset: -40% -10% auto auto;
      width: 380px;
      height: 380px;
      border-radius: 50%;
      background: radial-gradient(circle, rgba(255, 255, 255, 0.18), transparent 70%);
    }
    .eyebrow {
      font-size: 0.78rem;
      font-weight: 600;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      opacity: 0.8;
      margin: 0;
    }
    .hero h1 {
      font: 700 2rem/1.15 Inter, sans-serif;
      letter-spacing: -0.03em;
      margin: 6px 0;
    }
    .hero-text > p:not(.eyebrow) {
      margin: 0;
      opacity: 0.9;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
      margin-top: 20px;
    }
    .primary-action {
      --mat-button-filled-container-color: #fff;
      --mat-button-filled-label-text-color: #3730a3;
      font-weight: 600;
    }
    .ghost {
      --mat-button-outlined-label-text-color: #fff;
      --mat-button-outlined-outline-color: rgba(255, 255, 255, 0.55);
    }
    .hero-art {
      position: relative;
      width: 220px;
      height: 140px;
      flex: none;
    }
    .bubble {
      position: absolute;
      display: grid;
      place-items: center;
      border-radius: 18px;
      background: rgba(255, 255, 255, 0.16);
      backdrop-filter: blur(4px);
      border: 1px solid rgba(255, 255, 255, 0.25);
    }
    .b1 {
      width: 72px;
      height: 72px;
      left: 0;
      top: 30px;
    }
    .b2 {
      width: 88px;
      height: 88px;
      left: 70px;
      top: 0;
    }
    .b3 {
      width: 64px;
      height: 64px;
      left: 150px;
      top: 60px;
    }
    .bubble mat-icon {
      font-size: 30px;
      width: 30px;
      height: 30px;
    }
    .section-title {
      font: 600 1rem Inter, sans-serif;
      margin: 32px 0 14px;
      color: var(--app-muted);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 16px;
    }
    .tile {
      display: flex;
      align-items: flex-start;
      gap: 14px;
      padding: 18px;
      border-radius: var(--app-radius);
      background: var(--app-card-bg);
      border: 1px solid var(--app-border);
      box-shadow: var(--app-shadow);
      color: inherit;
      text-decoration: none;
      transition: transform 140ms ease, box-shadow 140ms ease, border-color 140ms;
    }
    .tile:hover,
    .tile:focus-visible {
      transform: translateY(-2px);
      border-color: color-mix(in srgb, var(--tint) 45%, transparent);
      box-shadow: 0 10px 24px color-mix(in srgb, var(--tint) 18%, transparent);
    }
    .chip {
      flex: none;
      width: 44px;
      height: 44px;
      border-radius: 12px;
      display: grid;
      place-items: center;
      color: var(--tint);
      background: color-mix(in srgb, var(--tint) 14%, transparent);
    }
    .tile-text {
      display: grid;
      gap: 4px;
      flex: 1;
    }
    .tile-text span {
      color: var(--app-muted);
      font-size: 0.88rem;
      line-height: 1.4;
    }
    .arrow {
      color: var(--app-muted);
      opacity: 0;
      transition: opacity 140ms, transform 140ms;
    }
    .tile:hover .arrow {
      opacity: 1;
      transform: translateX(2px);
    }
    @media (max-width: 799px) {
      .hero-art {
        display: none;
      }
      .hero {
        padding: 24px;
      }
    }
  `,
})
export class Home {
  protected readonly auth = inject(AuthService);
  protected readonly areas = computed(() => AREAS.filter((area) => this.auth.hasAnyRole(area.roles)));

  protected readonly greeting = computed(() => {
    const hour = new Date().getHours();
    return hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
  });

  protected has(path: string): boolean {
    return this.areas().some((a) => a.path === path);
  }

  protected tint(path: string): string {
    return AREA_TINT[path] ?? '#2563eb';
  }
}
