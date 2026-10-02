import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { CatalogApi } from '../../core/api/catalog.api';
import { categoryIcon } from '../../shared/ui/labels';
import { TranslatePipe } from '@ngx-translate/core';
import { EnumLabelPipe } from '../../shared/ui/enum-label';

/** Fold case and accents so "certificat" finds "Certificate" and "conge" finds "congé". */
const fold = (text: string) => text.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();

/** The request catalog: search as you type and filter by category. */
@Component({
  selector: 'app-catalog',
  imports: [MatCardModule, MatChipsModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule, RouterLink, TranslatePipe, EnumLabelPipe],
  template: `
    <h1>{{ 'catalog.title' | translate }}</h1>

    <a class="help-banner" routerLink="/portal/help" data-testid="help-banner">
      <mat-icon fontSet="material-symbols-outlined">lightbulb</mat-icon>
      <span>{{ 'catalog.helpBanner' | translate }}</span>
      <mat-icon class="flip-rtl" fontSet="material-symbols-outlined">arrow_forward</mat-icon>
    </a>

    <mat-form-field appearance="outline" class="search">
      <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
      <mat-label>{{ 'catalog.search' | translate }}</mat-label>
      <input matInput [value]="search()" (input)="search.set($any($event.target).value)" data-testid="catalog-search" />
    </mat-form-field>

    <mat-chip-listbox [attr.aria-label]="'catalog.category' | translate" [value]="category()" (change)="category.set($event.value ?? null)">
      @for (c of categories(); track c) {
        <mat-chip-option [value]="c">{{ c | enumLabel: 'category' }}</mat-chip-option>
      }
    </mat-chip-listbox>

    @if (types.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="grid">
      @for (type of visible(); track type.id) {
        <a class="tile" [routerLink]="['/portal/new', type.id]" [attr.data-testid]="'type-' + type.name">
          <mat-card appearance="outlined">
            <mat-card-content>
              <div class="head">
                <mat-icon fontSet="material-symbols-outlined">{{ icon(type.category) }}</mat-icon>
                <span class="category">{{ type.category | enumLabel: 'category' }}</span>
                @if (type.isConfidential) {
                  <span class="confidential"><mat-icon fontSet="material-symbols-outlined">lock</mat-icon>{{ 'catalog.confidential' | translate }}</span>
                }
              </div>
              <h2>{{ type.name }}</h2>
              <p>{{ type.description }}</p>
            </mat-card-content>
          </mat-card>
        </a>
      } @empty {
        @if (!types.isLoading()) {
          <p class="empty">{{ 'catalog.noMatch' | translate }}</p>
        }
      }
    </div>
  `,
  styles: `
    .help-banner {
      display: flex;
      align-items: center;
      gap: 10px;
      max-width: 640px;
      padding: 10px 14px;
      margin-bottom: 16px;
      border-radius: 12px;
      color: var(--mat-sys-on-tertiary-container);
      background: var(--mat-sys-tertiary-container);
      text-decoration: none;
      font-weight: 500;
    }
    .help-banner span {
      flex: 1;
    }
    .search {
      width: 100%;
      max-width: 480px;
    }
    mat-chip-listbox {
      display: block;
      margin-bottom: 16px;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: 16px;
    }
    .tile {
      color: inherit;
      text-decoration: none;
    }
    .tile mat-card {
      height: 100%;
    }
    .tile:hover mat-card,
    .tile:focus-visible mat-card {
      border-color: var(--mat-sys-primary);
    }
    .head {
      display: flex;
      align-items: center;
      gap: 8px;
      color: var(--mat-sys-primary);
    }
    .category {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .confidential {
      margin-left: auto;
      display: inline-flex;
      align-items: center;
      gap: 2px;
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-error);
    }
    .confidential mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    h2 {
      font: var(--mat-sys-title-medium);
      margin: 12px 0 4px;
    }
    .tile p {
      color: var(--mat-sys-on-surface-variant);
      margin: 0;
    }
  `,
})
export class Catalog {
  private readonly api = inject(CatalogApi);

  protected readonly types = rxResource({ stream: () => this.api.list() });
  protected readonly search = signal('');
  protected readonly category = signal<string | null>(null);

  protected readonly categories = computed(() => [...new Set((this.types.value() ?? []).map((t) => t.category))]);
  protected readonly visible = computed(() => {
    const term = fold(this.search().trim());
    const category = this.category();
    return (this.types.value() ?? []).filter(
      (t) => (!category || t.category === category) && (!term || fold(`${t.name} ${t.description}`).includes(term)),
    );
  });

  protected readonly icon = categoryIcon;
}
