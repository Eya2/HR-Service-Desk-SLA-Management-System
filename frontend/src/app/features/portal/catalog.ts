import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatButtonModule } from '@angular/material/button';
import { Router, RouterLink } from '@angular/router';
import { CatalogApi } from '../../core/api/catalog.api';
import { AssistantApi, RequestPrefillStore } from '../../core/api/assistant.api';
import { Classification, RequestTypeOption } from '../../core/api/api.models';
import { problemMessage } from '../../core/http/error.interceptor';
import { categoryIcon } from '../../shared/ui/labels';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { EnumLabelPipe } from '../../shared/ui/enum-label';

/** Fold case and accents so "certificat" finds "Certificate" and "conge" finds "congé". */
const fold = (text: string) => text.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();

/** The request catalog: search as you type and filter by category. */
@Component({
  selector: 'app-catalog',
  imports: [MatButtonModule, MatCardModule, MatChipsModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule, RouterLink, TranslatePipe, EnumLabelPipe],
  template: `
    <h1>{{ 'catalog.title' | translate }}</h1>

    <section class="assistant" aria-labelledby="assistant-title" data-testid="assistant">
      <h2 id="assistant-title">
        <mat-icon fontSet="material-symbols-outlined">auto_awesome</mat-icon>
        {{ 'assistant.title' | translate }}
      </h2>
      <p class="hint">{{ 'assistant.hint' | translate }}</p>
      <form class="ask" (submit)="$event.preventDefault(); ask()">
        <textarea
          [value]="need()"
          (input)="need.set($any($event.target).value)"
          (keydown.control.enter)="ask()"
          (keydown.meta.enter)="ask()"
          rows="2"
          maxlength="2000"
          [attr.aria-label]="'assistant.title' | translate"
          [placeholder]="'assistant.placeholder' | translate"
          data-testid="assistant-input"
        ></textarea>
        <button mat-flat-button type="submit" [disabled]="need().trim().length < 5 || thinking()" data-testid="assistant-ask">
          <mat-icon fontSet="material-symbols-outlined">{{ thinking() ? 'hourglass_top' : 'auto_awesome' }}</mat-icon>
          {{ (thinking() ? 'assistant.thinking' : 'assistant.ask') | translate }}
        </button>
      </form>

      @if (askError(); as message) {
        <p class="error" role="alert">{{ message }}</p>
      }

      @if (result(); as r) {
        <div class="result" aria-live="polite" data-testid="assistant-result">
          @if (r.suggestion; as s) {
            <div class="pick">
              <div class="pick-head">
                <mat-icon fontSet="material-symbols-outlined">{{ icon(s.category) }}</mat-icon>
                <div class="pick-name">
                  <small>{{ 'assistant.suggested' | translate }}</small>
                  <strong data-testid="assistant-type">{{ s.name }}</strong>
                </div>
                <span [class]="'match ' + level(s.confidence)" data-testid="assistant-match">{{ 'assistant.match.' + level(s.confidence) | translate }}</span>
              </div>
              <p class="pick-title" dir="auto">« {{ s.title }} »</p>
              @if (s.reason) {
                <p class="reason" dir="auto">{{ s.reason }}</p>
              }
              <p class="meta">
                @if (prefilled(s.values) > 0) {
                  <span><mat-icon fontSet="material-symbols-outlined">edit_note</mat-icon>{{ 'assistant.prefilled' | translate: { count: prefilled(s.values) } }}</span>
                }
                @if (s.isConfidential) {
                  <span class="lock"><mat-icon fontSet="material-symbols-outlined">lock</mat-icon>{{ 'assistant.confidential' | translate }}</span>
                }
                <span class="source">{{ 'assistant.source.' + r.source | translate }}</span>
              </p>
              <div class="pick-actions">
                <button mat-flat-button type="button" (click)="continueWith(s.requestTypeId, s.title, s.values)" data-testid="assistant-continue">
                  {{ 'assistant.continue' | translate }}
                  <mat-icon class="flip-rtl" iconPositionEnd fontSet="material-symbols-outlined">arrow_forward</mat-icon>
                </button>
                @if (r.alternatives.length > 0) {
                  <span class="or">{{ 'assistant.orMaybe' | translate }}</span>
                  @for (a of r.alternatives; track a.id) {
                    <button mat-stroked-button type="button" (click)="continueWithOther(a, s.title)">{{ a.name }}</button>
                  }
                }
              </div>
            </div>
          } @else {
            <p class="none" data-testid="assistant-none">{{ 'assistant.none' | translate }}</p>
          }

          @if (r.articles.length > 0) {
            <div class="answers">
              <p class="a-title"><mat-icon fontSet="material-symbols-outlined">lightbulb</mat-icon>{{ 'assistant.articles' | translate }}</p>
              @for (a of r.articles; track a.id) {
                <a [routerLink]="['/portal/help', a.id]" dir="auto">{{ a.title }}</a>
              }
            </div>
          }
        </div>
      }
    </section>

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
    .assistant {
      max-width: 860px;
      padding: 18px 20px;
      margin-bottom: 20px;
      border-radius: 18px;
      border: 1px solid color-mix(in srgb, var(--mat-sys-primary) 25%, transparent);
      background:
        radial-gradient(120% 140% at 0% 0%, color-mix(in srgb, var(--mat-sys-primary) 12%, transparent), transparent 60%),
        var(--app-card-bg, var(--mat-sys-surface));
    }
    .assistant h2 {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0;
      font: var(--mat-sys-title-medium);
      font-weight: 700;
      color: var(--mat-sys-primary);
    }
    .hint {
      margin: 4px 0 12px;
      color: var(--mat-sys-on-surface-variant);
    }
    .ask {
      display: flex;
      gap: 10px;
      align-items: stretch;
    }
    .ask textarea {
      flex: 1;
      min-height: 52px;
      padding: 12px 14px;
      resize: vertical;
      font: inherit;
      color: inherit;
      border-radius: 12px;
      border: 1px solid var(--mat-sys-outline-variant);
      background: var(--mat-sys-surface);
      outline: none;
    }
    .ask textarea:focus {
      border-color: var(--mat-sys-primary);
      box-shadow: 0 0 0 3px color-mix(in srgb, var(--mat-sys-primary) 18%, transparent);
    }
    .ask button {
      align-self: flex-end;
      height: 48px;
    }
    @media (max-width: 600px) {
      .ask {
        flex-direction: column;
      }
      .ask button {
        align-self: stretch;
      }
    }
    .result {
      display: grid;
      gap: 12px;
      margin-top: 14px;
    }
    .pick {
      padding: 14px 16px;
      border-radius: 14px;
      background: var(--mat-sys-surface);
      border: 1px solid var(--mat-sys-outline-variant);
    }
    .pick-head {
      display: flex;
      align-items: center;
      gap: 10px;
      color: var(--mat-sys-primary);
    }
    .pick-name {
      display: grid;
      flex: 1;
    }
    .pick-name small {
      color: var(--mat-sys-on-surface-variant);
    }
    .pick-name strong {
      font: var(--mat-sys-title-medium);
      font-weight: 700;
      color: var(--mat-sys-on-surface);
    }
    .match {
      padding: 2px 10px;
      border-radius: 999px;
      font: var(--mat-sys-label-medium);
    }
    .match.high {
      background: color-mix(in srgb, #1e8e3e 16%, transparent);
      color: light-dark(#1e6b32, #8fd6a0);
    }
    .match.medium {
      background: color-mix(in srgb, var(--mat-sys-primary) 14%, transparent);
      color: var(--mat-sys-primary);
    }
    .match.low {
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface-variant);
    }
    .pick-title {
      margin: 10px 0 2px;
      font-weight: 500;
    }
    .reason {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .meta {
      display: flex;
      flex-wrap: wrap;
      gap: 6px 16px;
      align-items: center;
      margin: 8px 0 12px;
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .meta span {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
    .meta mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .meta .lock {
      color: var(--mat-sys-error);
    }
    .meta .source {
      margin-inline-start: auto;
      font-style: italic;
    }
    .pick-actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .or {
      margin-inline-start: 8px;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
    }
    .none {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .answers {
      display: grid;
      gap: 4px;
    }
    .a-title {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0 0 2px;
      font-weight: 600;
      color: var(--mat-sys-tertiary);
    }
    .answers a {
      color: var(--mat-sys-primary);
      padding-inline-start: 30px;
    }
    .error {
      color: var(--mat-sys-error);
    }
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
  private readonly assistant = inject(AssistantApi);
  private readonly prefill = inject(RequestPrefillStore);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  /** What the employee needs, in their own words. */
  protected readonly need = signal('');
  protected readonly thinking = signal(false);
  protected readonly result = signal<Classification | null>(null);
  protected readonly askError = signal<string | null>(null);

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

  protected ask(): void {
    const text = this.need().trim();
    if (text.length < 5 || this.thinking()) return;
    this.thinking.set(true);
    this.askError.set(null);
    this.assistant.classify(text).subscribe({
      next: (result) => {
        this.result.set(result);
        this.thinking.set(false);
      },
      error: (error: unknown) => {
        this.askError.set(problemMessage(this.translate, error, 'assistant.failed'));
        this.thinking.set(false);
      },
    });
  }

  protected level(confidence: number): 'high' | 'medium' | 'low' {
    return confidence >= 0.75 ? 'high' : confidence >= 0.45 ? 'medium' : 'low';
  }

  protected prefilled(values: Record<string, unknown>): number {
    return Object.keys(values).length;
  }

  protected continueWith(requestTypeId: string, title: string, values: Record<string, unknown>): void {
    this.prefill.set({ requestTypeId, title, description: this.need().trim(), values });
    void this.router.navigate(['/portal/new', requestTypeId]);
  }

  /** Another type: the title and description still help, the answers belonged to the suggested form. */
  protected continueWithOther(type: RequestTypeOption, title: string): void {
    this.continueWith(type.id, title, {});
  }
}
