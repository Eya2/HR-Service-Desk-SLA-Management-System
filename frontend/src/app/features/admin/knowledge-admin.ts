import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CATEGORIES, ArticleSummary } from '../../core/api/api.models';
import { KnowledgeApi } from '../../core/api/knowledge.api';
import { problemMessage } from '../../core/http/error.interceptor';
import { EnumLabelPipe } from '../../shared/ui/enum-label';

/** Help centre editor: every article (drafts included) with its reads and "helpful" votes, and a side editor. */
@Component({
  selector: 'app-knowledge-admin',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    TranslatePipe,
    EnumLabelPipe,
  ],
  template: `
    <div class="page-header">
      <div>
        <h1>{{ 'kbAdmin.title' | translate }}</h1>
        <p>{{ 'kbAdmin.lead' | translate }}</p>
      </div>
      <button mat-flat-button type="button" (click)="startNew()" data-testid="new-article">
        <mat-icon fontSet="material-symbols-outlined">add</mat-icon> {{ 'kbAdmin.new' | translate }}
      </button>
    </div>

    @if (articles.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="layout">
      <div class="list">
        @for (a of articles.value() ?? []; track a.id) {
          <button type="button" class="row" [class.selected]="editingId() === a.id" (click)="edit(a)" [attr.data-testid]="'kb-' + a.title">
            <span class="row-main">
              <strong>{{ a.title }}</strong>
              <span class="row-meta">
                <span class="pill" [class.live]="a.isPublished">{{ (a.isPublished ? 'common.published' : 'common.draft') | translate }}</span>
                @if (a.category) {
                  <span>{{ a.category | enumLabel: 'category' }}</span>
                }
              </span>
            </span>
            <span class="stats">
              <span [title]="'kbAdmin.views' | translate"><mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>{{ a.viewCount }}</span>
              <span [title]="'kbAdmin.helpful' | translate"><mat-icon fontSet="material-symbols-outlined">thumb_up</mat-icon>{{ a.helpfulCount }}</span>
            </span>
          </button>
        } @empty {
          @if (!articles.isLoading()) {
            <p class="empty">{{ 'kbAdmin.empty' | translate }}</p>
          }
        }
      </div>

      @if (open()) {
        <form class="editor" [formGroup]="form" (ngSubmit)="save()" data-testid="article-editor">
          <h2>{{ (editingId() ? 'kbAdmin.edit' : 'kbAdmin.new') | translate }}</h2>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'kbAdmin.fieldTitle' | translate }}</mat-label>
            <input matInput formControlName="title" maxlength="200" required data-testid="kb-title" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'kbAdmin.summary' | translate }}</mat-label>
            <input matInput formControlName="summary" maxlength="300" />
            <mat-hint>{{ 'kbAdmin.summaryHint' | translate }}</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'kbAdmin.category' | translate }}</mat-label>
            <mat-select formControlName="category">
              <mat-option [value]="null">{{ 'common.none' | translate }}</mat-option>
              @for (c of categories; track c) {
                <mat-option [value]="c">{{ c | enumLabel: 'category' }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'kbAdmin.body' | translate }}</mat-label>
            <textarea matInput formControlName="body" rows="12" maxlength="8000" required data-testid="kb-body"></textarea>
            <mat-hint>{{ 'kbAdmin.bodyHint' | translate }}</mat-hint>
            <mat-hint align="end">{{ form.controls.body.value.length }} / 8000</mat-hint>
          </mat-form-field>
          <mat-slide-toggle formControlName="isPublished" data-testid="kb-published">{{ 'kbAdmin.publish' | translate }}</mat-slide-toggle>
          <div class="actions">
            <button mat-button type="button" (click)="open.set(false)">{{ 'common.cancel' | translate }}</button>
            <button mat-flat-button type="submit" [disabled]="form.invalid || saving()" data-testid="kb-save">{{ 'common.save' | translate }}</button>
          </div>
        </form>
      }
    </div>
  `,
  styles: `
    .layout {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1.1fr);
      gap: 20px;
      align-items: start;
    }
    .list {
      display: grid;
      gap: 8px;
    }
    .row {
      display: flex;
      align-items: center;
      gap: 12px;
      width: 100%;
      padding: 12px 14px;
      border-radius: 12px;
      border: 1px solid var(--app-border);
      background: var(--app-card-bg);
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .row:hover,
    .row.selected {
      border-color: color-mix(in srgb, var(--mat-sys-primary) 45%, transparent);
    }
    .row.selected {
      background: color-mix(in srgb, var(--mat-sys-primary) 6%, var(--app-card-bg));
    }
    .row-main {
      display: grid;
      gap: 4px;
      flex: 1;
      min-width: 0;
    }
    .row-meta {
      display: flex;
      gap: 8px;
      align-items: center;
      font-size: 0.8rem;
      color: var(--app-muted);
    }
    .pill {
      padding: 1px 8px;
      border-radius: 10px;
      font-weight: 600;
      background: var(--mat-sys-surface-container-high);
    }
    .pill.live {
      color: var(--mat-sys-on-primary-container);
      background: var(--mat-sys-primary-container);
    }
    .stats {
      display: flex;
      gap: 12px;
      color: var(--app-muted);
      font-size: 0.85rem;
    }
    .stats span {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
    .stats mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .editor {
      display: grid;
      gap: 6px;
      padding: 20px;
      border-radius: 16px;
      border: 1px solid var(--app-border);
      background: var(--app-card-bg);
      position: sticky;
      top: 72px;
    }
    .editor h2 {
      font: 600 1.1rem Inter, 'IBM Plex Sans Arabic', sans-serif;
      margin: 0 0 8px;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
      margin-top: 8px;
    }
    .empty {
      color: var(--app-muted);
    }
    @media (max-width: 959px) {
      .layout {
        grid-template-columns: 1fr;
      }
      .editor {
        position: static;
      }
    }
  `,
})
export class KnowledgeAdmin {
  private readonly api = inject(KnowledgeApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  protected readonly categories = CATEGORIES;
  protected readonly articles = rxResource({ stream: () => this.api.list(null, true) });
  protected readonly open = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly selected = computed(() => this.articles.value()?.find((a) => a.id === this.editingId()) ?? null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    summary: ['', Validators.maxLength(300)],
    category: [null as string | null],
    body: ['', [Validators.required, Validators.maxLength(8000)]],
    isPublished: [false],
  });

  protected startNew(): void {
    this.editingId.set(null);
    this.form.reset({ title: '', summary: '', category: null, body: '', isPublished: false });
    this.open.set(true);
  }

  /** The list carries no body: load the full article. */
  protected edit(summary: ArticleSummary): void {
    this.editingId.set(summary.id);
    this.open.set(true);
    this.api.get(summary.id).subscribe((a) =>
      this.form.reset({ title: a.title, summary: a.summary, category: a.category, body: a.body, isPublished: a.isPublished }),
    );
  }

  protected save(): void {
    this.saving.set(true);
    const value = this.form.getRawValue();
    this.api.save(this.editingId(), { ...value, summary: value.summary.trim() }).subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.editingId.set(saved.id);
        this.snackBar.open(this.translate.instant('common.saved'), undefined, { duration: 3000 });
        this.articles.reload();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.snackBar.open(problemMessage(this.translate, error), this.translate.instant('common.dismiss'), { duration: 6000 });
      },
    });
  }
}
