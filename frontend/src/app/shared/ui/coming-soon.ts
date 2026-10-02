import { Component, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

/** Placeholder for a work area whose screens arrive in a later phase of the roadmap. */
@Component({
  selector: 'app-coming-soon',
  imports: [MatCardModule, MatIconModule],
  template: `
    <h1>{{ title() }}</h1>
    <mat-card appearance="outlined">
      <mat-card-content class="body">
        <mat-icon fontSet="material-symbols-outlined">construction</mat-icon>
        <p>{{ description() }}</p>
        <p class="phase">Planned for phase {{ phase() }} of the roadmap.</p>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
    .body {
      display: grid;
      justify-items: center;
      text-align: center;
      padding: 32px 16px;
    }
    .body mat-icon {
      font-size: 40px;
      width: 40px;
      height: 40px;
      color: var(--mat-sys-primary);
    }
    .phase {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class ComingSoon {
  readonly title = input.required<string>();
  readonly description = input<string>('');
  readonly phase = input.required<number>();
}
