import { Component, computed, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { AREAS } from '../../core/auth/areas';
import { AuthService } from '../../core/auth/auth.service';

/** Landing page: greets the user and links to the areas their roles open. */
@Component({
  selector: 'app-home',
  imports: [MatCardModule, MatIconModule, RouterLink],
  template: `
    <h1>Welcome, {{ auth.user()?.firstName }}</h1>
    <p class="subtitle">{{ auth.user()?.tenantName }}</p>

    @if (areas().length > 0) {
      <div class="grid">
        @for (area of areas(); track area.path) {
          <a class="tile" [routerLink]="['/', area.path]" [attr.data-testid]="'area-' + area.path">
            <mat-card appearance="outlined">
              <mat-card-content>
                <mat-icon fontSet="material-symbols-outlined">{{ area.icon }}</mat-icon>
                <h2>{{ area.label }}</h2>
                <p>{{ area.description }}</p>
              </mat-card-content>
            </mat-card>
          </a>
        }
      </div>
    } @else {
      <p>No work area is available for your role yet.</p>
    }
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-medium);
      margin-bottom: 4px;
    }
    .subtitle {
      color: var(--mat-sys-on-surface-variant);
      margin-top: 0;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: 16px;
      margin-top: 24px;
    }
    .tile {
      color: inherit;
      text-decoration: none;
    }
    .tile mat-card {
      height: 100%;
      transition: border-color 120ms;
    }
    .tile:hover mat-card,
    .tile:focus-visible mat-card {
      border-color: var(--mat-sys-primary);
    }
    .tile mat-icon {
      color: var(--mat-sys-primary);
    }
    h2 {
      font: var(--mat-sys-title-medium);
      margin: 8px 0 4px;
    }
    .tile p {
      color: var(--mat-sys-on-surface-variant);
      margin: 0;
    }
  `,
})
export class Home {
  protected readonly auth = inject(AuthService);
  protected readonly areas = computed(() => AREAS.filter((area) => this.auth.hasAnyRole(area.roles)));
}
