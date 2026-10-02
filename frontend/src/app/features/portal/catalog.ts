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
import { categoryIcon, categoryLabel } from '../../shared/ui/labels';

/** Fold case and accents so "certificat" finds "Certificate" and "conge" finds "congé". */
const fold = (text: string) => text.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();

/** The request catalog: search as you type and filter by category. */
@Component({
  selector: 'app-catalog',
  imports: [MatCardModule, MatChipsModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule, RouterLink],
  template: `
    <h1>What do you need?</h1>

    <mat-form-field appearance="outline" class="search">
      <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
      <mat-label>Search the catalog</mat-label>
      <input matInput [value]="search()" (input)="search.set($any($event.target).value)" data-testid="catalog-search" />
    </mat-form-field>

    <mat-chip-listbox aria-label="Category" [value]="category()" (change)="category.set($event.value ?? null)">
      @for (c of categories(); track c) {
        <mat-chip-option [value]="c">{{ label(c) }}</mat-chip-option>
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
                <span class="category">{{ label(type.category) }}</span>
                @if (type.isConfidential) {
                  <span class="confidential"><mat-icon fontSet="material-symbols-outlined">lock</mat-icon>Confidential</span>
                }
              </div>
              <h2>{{ type.name }}</h2>
              <p>{{ type.description }}</p>
            </mat-card-content>
          </mat-card>
        </a>
      } @empty {
        @if (!types.isLoading()) {
          <p class="empty">No request matches your search.</p>
        }
      }
    </div>
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
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

  protected readonly label = categoryLabel;
  protected readonly icon = categoryIcon;
}
