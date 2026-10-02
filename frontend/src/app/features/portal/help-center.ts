import { Component, computed, inject, signal } from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { KnowledgeApi } from '../../core/api/knowledge.api';
import { EnumLabelPipe } from '../../shared/ui/enum-label';
import { categoryIcon } from '../../shared/ui/labels';

/** Help centre: search the HR articles before opening a request. */
@Component({
  selector: 'app-help-center',
  imports: [MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule, RouterLink, TranslatePipe, EnumLabelPipe],
  template: `
    <section class="search-hero">
      <h1>{{ 'help.title' | translate }}</h1>
      <p>{{ 'help.lead' | translate }}</p>
      <mat-form-field appearance="outline" class="search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <mat-label>{{ 'help.search' | translate }}</mat-label>
        <input matInput [value]="query()" (input)="query.set($any($event.target).value)" data-testid="help-search" />
      </mat-form-field>
    </section>

    @if (articles.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    @if (articles.value(); as list) {
      <p class="count" aria-live="polite">
        {{ (searching() ? 'help.results' : 'help.all') | translate: { count: list.length } }}
      </p>
      <div class="list">
        @for (a of list; track a.id) {
          <a class="article" [routerLink]="['/portal/help', a.id]" [attr.data-testid]="'article-' + a.id">
            <span class="icon"><mat-icon fontSet="material-symbols-outlined">{{ icon(a.category) }}</mat-icon></span>
            <span class="text">
              <strong dir="auto">{{ a.title }}</strong>
              <span dir="auto">{{ a.summary }}</span>
              @if (a.category) {
                <small>{{ a.category | enumLabel: 'category' }}</small>
              }
            </span>
            <mat-icon class="chevron flip-rtl" fontSet="material-symbols-outlined">chevron_right</mat-icon>
          </a>
        } @empty {
          <div class="empty">
            <mat-icon fontSet="material-symbols-outlined">search_off</mat-icon>
            <p>{{ 'help.noResult' | translate }}</p>
            <a routerLink="/portal/catalog">{{ 'help.openRequest' | translate }}</a>
          </div>
        }
      </div>
    }
  `,
  styles: `
    .search-hero {
      padding: 28px;
      border-radius: 20px;
      background:
        radial-gradient(circle at 90% 10%, color-mix(in srgb, var(--mat-sys-tertiary) 18%, transparent), transparent 55%),
        color-mix(in srgb, var(--mat-sys-primary) 8%, var(--app-card-bg));
      border: 1px solid var(--app-border);
      margin-bottom: 20px;
    }
    h1 {
      font: 700 1.6rem/1.2 Inter, 'IBM Plex Sans Arabic', sans-serif;
      letter-spacing: -0.02em;
      margin: 0 0 6px;
    }
    .search-hero p {
      color: var(--app-muted);
      margin: 0 0 16px;
    }
    .search {
      width: 100%;
      max-width: 640px;
    }
    .count {
      color: var(--app-muted);
      font-size: 0.85rem;
    }
    .list {
      display: grid;
      gap: 10px;
    }
    .article {
      display: flex;
      align-items: center;
      gap: 14px;
      padding: 14px 16px;
      border-radius: 14px;
      border: 1px solid var(--app-border);
      background: var(--app-card-bg);
      color: inherit;
      text-decoration: none;
      transition: border-color 120ms, box-shadow 120ms;
    }
    .article:hover,
    .article:focus-visible {
      border-color: color-mix(in srgb, var(--mat-sys-primary) 40%, transparent);
      box-shadow: 0 6px 18px color-mix(in srgb, var(--mat-sys-primary) 10%, transparent);
    }
    .icon {
      flex: none;
      width: 40px;
      height: 40px;
      border-radius: 12px;
      display: grid;
      place-items: center;
      color: var(--mat-sys-primary);
      background: color-mix(in srgb, var(--mat-sys-primary) 12%, transparent);
    }
    .text {
      display: grid;
      gap: 2px;
      flex: 1;
      min-width: 0;
    }
    .text span {
      color: var(--app-muted);
      font-size: 0.9rem;
    }
    .text small {
      color: var(--mat-sys-primary);
      font-weight: 600;
      font-size: 0.75rem;
    }
    .chevron {
      color: var(--app-muted);
    }
    .empty {
      text-align: center;
      padding: 32px;
      color: var(--app-muted);
    }
  `,
})
export class HelpCenter {
  private readonly api = inject(KnowledgeApi);

  protected readonly query = signal('');
  private readonly debounced = toSignal(toObservable(this.query).pipe(debounceTime(250), distinctUntilChanged()), { initialValue: '' });
  protected readonly searching = computed(() => this.debounced().trim().length > 0);
  protected readonly articles = rxResource({ params: () => this.debounced(), stream: ({ params }) => this.api.list(params) });
  protected readonly icon = (category: string | null) => (category ? categoryIcon(category) : 'article');
}
