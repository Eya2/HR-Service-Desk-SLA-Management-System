import { Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SystemApi } from '../../core/api/system-api';

/** Temporary landing page that proves the SPA can reach the API. Replaced by role-based landing in phase 2. */
@Component({
  selector: 'app-home',
  imports: [MatCardModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <h1>Welcome to the HR Service Desk</h1>
    <mat-card appearance="outlined">
      <mat-card-header>
        <mat-card-title>API status</mat-card-title>
      </mat-card-header>
      <mat-card-content>
        @if (info.isLoading()) {
          <mat-spinner diameter="24" />
        } @else if (info.error()) {
          <p class="status down" data-testid="api-status">
            <mat-icon fontSet="material-symbols-outlined">error</mat-icon> API unreachable
          </p>
        } @else if (info.value(); as value) {
          <p class="status up" data-testid="api-status">
            <mat-icon fontSet="material-symbols-outlined">check_circle</mat-icon>
            {{ value.name }} · {{ value.version }} · {{ value.environment }}
          </p>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-medium);
    }
    .status {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .up mat-icon {
      color: var(--mat-sys-primary);
    }
    .down mat-icon {
      color: var(--mat-sys-error);
    }
  `,
})
export class Home {
  private readonly systemApi = inject(SystemApi);

  protected readonly info = rxResource({ stream: () => this.systemApi.getInfo() });
}
