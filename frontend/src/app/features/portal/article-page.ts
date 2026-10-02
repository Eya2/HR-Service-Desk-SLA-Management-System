import { DatePipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { KnowledgeApi } from '../../core/api/knowledge.api';
import { EnumLabelPipe } from '../../shared/ui/enum-label';

/** One help article, with "This answered my question" and a way to still open a request. */
@Component({
  selector: 'app-article-page',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink, TranslatePipe, EnumLabelPipe],
  template: `
    <a mat-button routerLink="/portal/help" class="back">
      <mat-icon class="flip-rtl" fontSet="material-symbols-outlined">arrow_back</mat-icon> {{ 'help.title' | translate }}
    </a>

    @if (article.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (article.error()) {
      <p class="empty">{{ 'errors.knowledge.not_found' | translate }}</p>
    } @else if (article.value(); as a) {
      <article class="card">
        @if (a.category) {
          <span class="eyebrow">{{ a.category | enumLabel: 'category' }}</span>
        }
        <h1 dir="auto">{{ a.title }}</h1>
        @if (a.summary) {
          <p class="summary" dir="auto">{{ a.summary }}</p>
        }
        <div class="body" data-testid="article-body">
          @for (paragraph of paragraphs(a.body); track $index) {
            <p dir="auto">{{ paragraph }}</p>
          }
        </div>
        @if (a.updatedAt) {
          <p class="meta">{{ 'help.updated' | translate: { date: (a.updatedAt | date: 'mediumDate') } }}</p>
        }
      </article>

      <section class="feedback" aria-live="polite">
        @if (voted()) {
          <p class="thanks" data-testid="helpful-thanks">
            <mat-icon fontSet="material-symbols-outlined">task_alt</mat-icon> {{ 'help.thanks' | translate }}
          </p>
        } @else {
          <span>{{ 'help.answered' | translate }}</span>
          <button mat-flat-button (click)="helpful(a.id)" data-testid="helpful">
            <mat-icon fontSet="material-symbols-outlined">thumb_up</mat-icon> {{ 'help.yesAnswered' | translate }}
          </button>
          <a mat-stroked-button routerLink="/portal/catalog">{{ 'help.stillNeed' | translate }}</a>
        }
      </section>
    }
  `,
  styles: `
    .back {
      margin-bottom: 8px;
    }
    .card {
      max-width: 780px;
      padding: 28px 32px;
      border-radius: 18px;
      border: 1px solid var(--app-border);
      background: var(--app-card-bg);
    }
    .eyebrow {
      font-size: 0.75rem;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--mat-sys-primary);
    }
    h1 {
      font: 700 1.7rem/1.25 Inter, 'IBM Plex Sans Arabic', sans-serif;
      letter-spacing: -0.02em;
      margin: 6px 0 8px;
    }
    .summary {
      font-size: 1.05rem;
      color: var(--app-muted);
    }
    .body p {
      line-height: 1.7;
    }
    .meta {
      color: var(--app-muted);
      font-size: 0.8rem;
      margin-bottom: 0;
    }
    .feedback {
      max-width: 780px;
      display: flex;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
      margin-top: 16px;
      padding: 16px 20px;
      border-radius: 14px;
      background: color-mix(in srgb, var(--mat-sys-primary) 7%, transparent);
    }
    .feedback span {
      font-weight: 600;
      margin-inline-end: auto;
    }
    .thanks {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0;
      color: var(--mat-sys-primary);
      font-weight: 600;
    }
    .empty {
      color: var(--app-muted);
    }
  `,
})
export class ArticlePage {
  private readonly api = inject(KnowledgeApi);

  /** Route parameter. */
  readonly articleId = input.required<string>();

  protected readonly article = rxResource({ params: () => this.articleId(), stream: ({ params }) => this.api.get(params) });
  protected readonly voted = signal(false);

  protected paragraphs(body: string): string[] {
    return body.split(/\n{2,}/).map((p) => p.trim()).filter((p) => p.length > 0);
  }

  protected helpful(id: string): void {
    this.voted.set(true);
    this.api.markHelpful(id).subscribe({ error: () => this.voted.set(false) });
  }
}
