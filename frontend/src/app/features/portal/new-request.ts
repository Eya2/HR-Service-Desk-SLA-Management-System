import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { CatalogApi } from '../../core/api/catalog.api';
import { TicketsApi } from '../../core/api/tickets.api';
import { problemMessage, problemOf } from '../../core/http/error.interceptor';
import { DynamicForm } from '../../shared/dynamic-form/dynamic-form';
import { DynamicFormGroup, buildFormGroup, toSubmission } from '../../shared/dynamic-form/form-builder';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged, map, of } from 'rxjs';
import { KnowledgeApi } from '../../core/api/knowledge.api';

/** Submission page: a title and description, then the request type's own dynamic form. */
@Component({
  selector: 'app-new-request',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    RouterLink,
    DynamicForm,
    TranslatePipe,
  ],
  template: `
    <a mat-button routerLink="/portal/catalog" class="back">
      <mat-icon class="flip-rtl" fontSet="material-symbols-outlined">arrow_back</mat-icon> {{ 'newRequest.catalog' | translate }}
    </a>

    @if (type.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (type.value(); as requestType) {
      <h1>{{ requestType.name }}</h1>
      <p class="description">{{ requestType.description }}</p>
      @if (requestType.isConfidential) {
        <p class="confidential" role="note">
          <mat-icon fontSet="material-symbols-outlined">lock</mat-icon>
          {{ 'newRequest.confidential' | translate }}
        </p>
      }

      <mat-card appearance="outlined">
        <mat-card-content>
          <!-- Two form groups (details + dynamic answers): handle the native submit event. -->
          <form (submit)="$event.preventDefault(); submit()" novalidate>
            <div [formGroup]="details" class="details">
              <mat-form-field appearance="outline">
                <mat-label>{{ 'newRequest.title' | translate }}</mat-label>
                <input matInput formControlName="title" required maxlength="200" />
                <mat-error>{{ titleError() }}</mat-error>
              </mat-form-field>
              @if (suggestions().length > 0) {
                <aside class="suggestions" data-testid="suggestions" aria-live="polite">
                  <p class="s-title">
                    <mat-icon fontSet="material-symbols-outlined">lightbulb</mat-icon> {{ 'help.suggestTitle' | translate }}
                  </p>
                  @for (a of suggestions(); track a.id) {
                    <a [routerLink]="['/portal/help', a.id]" target="_blank" class="s-item">
                      <strong dir="auto">{{ a.title }}</strong>
                      <span dir="auto">{{ a.summary }}</span>
                    </a>
                  }
                  <small>{{ 'help.suggestHint' | translate }}</small>
                </aside>
              }
              <mat-form-field appearance="outline">
                <mat-label>{{ 'newRequest.details' | translate }}</mat-label>
                <textarea matInput formControlName="description" rows="3" maxlength="4000"></textarea>
              </mat-form-field>
            </div>

            @if (answers(); as group) {
              <app-dynamic-form [fields]="requestType.fields" [group]="group" />
            }

            @if (error(); as message) {
              <p class="error" role="alert">{{ message }}</p>
            }

            <div class="actions">
              <button mat-flat-button type="submit" [disabled]="submitting()" data-testid="submit-request">
                {{ (submitting() ? 'newRequest.submitting' : 'newRequest.submit') | translate }}
              </button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `
    .back {
      margin-bottom: 8px;
    }
    h1 {
      font: var(--mat-sys-headline-small);
      margin-bottom: 4px;
    }
    .description {
      color: var(--mat-sys-on-surface-variant);
    }
    .confidential {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 12px;
      border-radius: 8px;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    mat-card {
      max-width: 720px;
    }
    .details {
      display: grid;
      gap: 8px;
    }
    .suggestions {
      display: grid;
      gap: 8px;
      padding: 14px 16px;
      margin: -4px 0 12px;
      border-radius: 14px;
      border: 1px solid color-mix(in srgb, var(--mat-sys-tertiary) 35%, transparent);
      background: color-mix(in srgb, var(--mat-sys-tertiary) 8%, transparent);
    }
    .s-title {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0;
      font-weight: 600;
      color: var(--mat-sys-tertiary);
    }
    .s-item {
      display: grid;
      gap: 2px;
      padding: 8px 10px;
      border-radius: 10px;
      color: inherit;
      text-decoration: none;
      background: var(--app-card-bg);
    }
    .s-item:hover {
      outline: 1px solid color-mix(in srgb, var(--mat-sys-tertiary) 45%, transparent);
    }
    .s-item span,
    .suggestions small {
      color: var(--app-muted);
      font-size: 0.85rem;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      margin-top: 16px;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class NewRequest {
  private readonly catalogApi = inject(CatalogApi);
  private readonly ticketsApi = inject(TicketsApi);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);
  private readonly knowledgeApi = inject(KnowledgeApi);

  /** Route parameter. */
  readonly typeId = input.required<string>();

  protected readonly type = rxResource({ params: () => this.typeId(), stream: ({ params }) => this.catalogApi.get(params) });
  protected readonly answers = computed<DynamicFormGroup | null>(() => {
    const type = this.type.value();
    return type ? buildFormGroup(type.fields) : null;
  });
  protected readonly details = inject(NonNullableFormBuilder).group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(4000)],
  });
  /** Deflection: help articles matching the title as it is typed. */
  private readonly titleText = toSignal(
    this.details.controls.title.valueChanges.pipe(
      map((t) => t.trim()),
      debounceTime(350),
      distinctUntilChanged(),
    ),
    { initialValue: '' },
  );
  private readonly suggested = rxResource({
    params: () => this.titleText(),
    stream: ({ params }) => (params.length >= 3 ? this.knowledgeApi.suggest(params) : of([])),
  });
  protected readonly suggestions = computed(() => this.suggested.value() ?? []);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    // Suggest the request type's name as the title.
    effect(() => {
      const type = this.type.value();
      if (type && !this.details.controls.title.dirty) this.details.controls.title.setValue(type.name);
    });
  }

  protected titleError(): string {
    const control = this.details.controls.title;
    return control.errors?.['server'] ?? this.translate.instant('newRequest.titleError');
  }

  protected submit(): void {
    const type = this.type.value();
    const answers = this.answers();
    if (!type || !answers) return;

    if (this.details.invalid || answers.invalid) {
      this.details.markAllAsTouched();
      answers.markAllAsTouched();
      this.error.set(this.translate.instant('common.fixFields'));
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const { values, files } = toSubmission(type.fields, answers);
    const { title, description } = this.details.getRawValue();

    this.ticketsApi.submit({ requestTypeId: type.id, title, description, values, files }).subscribe({
      next: (created) => {
        this.snackBar.open(this.translate.instant('newRequest.submitted', { reference: created.reference }), this.translate.instant('common.ok'), {
          duration: 5000,
        });
        void this.router.navigate(['/tickets', created.id]);
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.showServerErrors(error, answers);
      },
    });
  }

  /** Puts each server-side validation message on its field ("values.<key>" or "Title"). */
  private showServerErrors(error: unknown, answers: DynamicFormGroup): void {
    const problem = problemOf(error);
    if (!(error instanceof HttpErrorResponse) || error.status !== 400 || !problem?.errors) {
      this.error.set(problemMessage(this.translate, error, 'newRequest.failed'));
      return;
    }

    const unplaced: string[] = [];
    for (const [key, messages] of Object.entries(problem.errors)) {
      const control = key.startsWith('values.')
        ? answers.controls[key.slice('values.'.length)]
        : key.toLowerCase() === 'title'
          ? this.details.controls.title
          : null;
      if (control) {
        control.setErrors({ server: messages[0] });
        control.markAsTouched();
      } else {
        unplaced.push(...messages);
      }
    }
    this.error.set(unplaced.length > 0 ? unplaced.join(' ') : this.translate.instant('common.fixFields'));
  }
}
