import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from './language.service';

/** Language switcher for the top bar and the sign-in page. */
@Component({
  selector: 'app-language-menu',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, MatTooltipModule, TranslatePipe],
  template: `
    <button
      mat-button
      class="trigger"
      [matMenuTriggerFor]="menu"
      [matTooltip]="'language.choose' | translate"
      [attr.aria-label]="'language.choose' | translate"
      data-testid="language-menu"
    >
      <mat-icon fontSet="material-symbols-outlined">translate</mat-icon>
      <span class="code">{{ language.current().toUpperCase() }}</span>
    </button>
    <mat-menu #menu="matMenu">
      @for (option of language.languages; track option.code) {
        <button mat-menu-item (click)="language.change(option.code)" [attr.lang]="option.code" [attr.data-testid]="'language-' + option.code">
          <mat-icon fontSet="material-symbols-outlined">{{ option.code === language.current() ? 'check' : '' }}</mat-icon>
          <span>{{ option.nativeName }}</span>
        </button>
      }
    </mat-menu>
  `,
  styles: `
    .trigger {
      min-width: 0;
      color: var(--app-muted);
    }
    .code {
      font-weight: 600;
      font-size: 0.8rem;
      letter-spacing: 0.04em;
    }
  `,
})
export class LanguageMenu {
  protected readonly language = inject(LanguageService);
}
