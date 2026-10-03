import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CATEGORIES, FieldType, FormFieldDef, PRIORITIES, RequestTypeSummary } from '../../core/api/api.models';
import { CatalogApi } from '../../core/api/catalog.api';
import { problemMessage } from '../../core/http/error.interceptor';
import { DynamicForm } from '../../shared/dynamic-form/dynamic-form';
import { buildFormGroup } from '../../shared/dynamic-form/form-builder';
import { EnumLabelPipe } from '../../shared/ui/enum-label';
import { categoryIcon } from '../../shared/ui/labels';

/** A field being edited: its key follows the label until the admin types one. */
interface DraftField extends FormFieldDef {
  uid: number;
  keyTouched: boolean;
}

const FIELD_TYPES: { type: FieldType; icon: string }[] = [
  { type: 'Text', icon: 'short_text' },
  { type: 'Textarea', icon: 'notes' },
  { type: 'Number', icon: 'pin' },
  { type: 'Date', icon: 'calendar_month' },
  { type: 'Select', icon: 'list' },
  { type: 'File', icon: 'attach_file' },
];

/** "Licence plate number" → "licencePlateNumber" (letters, digits, starts with a letter, max 50). */
export function keyFromLabel(label: string): string {
  const words = label
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .replace(/[^A-Za-z0-9]+/g, ' ')
    .trim()
    .split(' ')
    .filter((w) => w.length > 0);
  const key = words.map((w, i) => (i === 0 ? w.toLowerCase() : w[0].toUpperCase() + w.slice(1).toLowerCase())).join('');
  return (/^[A-Za-z]/.test(key) ? key : `field${key}`).slice(0, 50);
}

/** HR Admin: request types and their forms, built visually with a live preview. */
@Component({
  selector: 'app-catalog-admin',
  imports: [
    FormsModule,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTooltipModule,
    TranslatePipe,
    EnumLabelPipe,
    DynamicForm,
  ],
  template: `
    <div class="page-header">
      <div>
        <h1>{{ 'catalogAdmin.title' | translate }}</h1>
        <p>{{ 'catalogAdmin.lead' | translate }}</p>
      </div>
      <button mat-flat-button type="button" (click)="startNew()" data-testid="new-type">
        <mat-icon fontSet="material-symbols-outlined">add</mat-icon> {{ 'catalogAdmin.new' | translate }}
      </button>
    </div>

    <div class="layout" [class.editing]="open()">
      <nav class="types" [attr.aria-label]="'catalogAdmin.title' | translate">
        @for (t of types.value() ?? []; track t.id) {
          <button type="button" class="type" [class.selected]="editingId() === t.id" [class.inactive]="!t.isActive" (click)="edit(t)" [attr.data-testid]="'type-' + t.name">
            <span class="type-icon"><mat-icon fontSet="material-symbols-outlined">{{ icon(t.category) }}</mat-icon></span>
            <span class="type-text">
              <strong>{{ t.name }}</strong>
              <small>{{ t.category | enumLabel: 'category' }}</small>
            </span>
            @if (t.isConfidential) {
              <mat-icon class="flag" fontSet="material-symbols-outlined" [matTooltip]="'catalogAdmin.confidential' | translate">lock</mat-icon>
            }
            @if (!t.isActive) {
              <span class="pill off">{{ 'catalogAdmin.retired' | translate }}</span>
            }
          </button>
        }
      </nav>

      @if (open()) {
        <section class="editor" data-testid="type-editor">
          <div class="builder">
            <div class="card">
              <h2>{{ (editingId() ? 'catalogAdmin.edit' : 'catalogAdmin.new') | translate }}</h2>
              <form class="settings" (submit)="$event.preventDefault()">
                <mat-form-field appearance="outline" class="wide">
                  <mat-label>{{ 'catalogAdmin.name' | translate }}</mat-label>
                  <input matInput [(ngModel)]="name" name="name" maxlength="100" required data-testid="type-name" />
                </mat-form-field>
                <mat-form-field appearance="outline" class="wide">
                  <mat-label>{{ 'catalogAdmin.description' | translate }}</mat-label>
                  <textarea matInput [(ngModel)]="description" name="description" rows="2" maxlength="1000"></textarea>
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>{{ 'catalogAdmin.category' | translate }}</mat-label>
                  <mat-select [(ngModel)]="category" name="category">
                    @for (c of categories; track c) {
                      <mat-option [value]="c">{{ c | enumLabel: 'category' }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>{{ 'catalogAdmin.priority' | translate }}</mat-label>
                  <mat-select [(ngModel)]="priority" name="priority">
                    @for (p of priorities; track p) {
                      <mat-option [value]="p">{{ p | enumLabel: 'priority' }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
              </form>
              <div class="toggles">
                <mat-slide-toggle [(ngModel)]="isConfidential" name="confidential">
                  <span class="toggle-text"><strong>{{ 'catalogAdmin.confidential' | translate }}</strong><small>{{ 'catalogAdmin.confidentialHint' | translate }}</small></span>
                </mat-slide-toggle>
                <mat-slide-toggle [(ngModel)]="isSensitive" name="sensitive">
                  <span class="toggle-text"><strong>{{ 'catalogAdmin.sensitive' | translate }}</strong><small>{{ 'catalogAdmin.sensitiveHint' | translate }}</small></span>
                </mat-slide-toggle>
              </div>
            </div>

            <div class="card">
              <div class="fields-head">
                <h2>{{ 'catalogAdmin.fields' | translate }} <span class="count">{{ fields().length }}</span></h2>
                <button mat-stroked-button type="button" [matMenuTriggerFor]="addMenu" data-testid="add-field">
                  <mat-icon fontSet="material-symbols-outlined">add</mat-icon> {{ 'catalogAdmin.addField' | translate }}
                </button>
                <mat-menu #addMenu="matMenu">
                  @for (t of fieldTypes; track t.type) {
                    <button mat-menu-item type="button" (click)="addField(t.type)" [attr.data-testid]="'add-' + t.type">
                      <mat-icon fontSet="material-symbols-outlined">{{ t.icon }}</mat-icon>
                      <span>{{ 'catalogAdmin.type.' + t.type | translate }}</span>
                    </button>
                  }
                </mat-menu>
              </div>

              <div class="fields" cdkDropList (cdkDropListDropped)="drop($event)">
                @for (f of fields(); track f.uid; let i = $index) {
                  <div class="field" cdkDrag [class.open]="expanded() === f.uid" [attr.data-testid]="'field-' + i">
                    <div class="field-head">
                      <mat-icon class="handle" cdkDragHandle fontSet="material-symbols-outlined" [matTooltip]="'catalogAdmin.drag' | translate">drag_indicator</mat-icon>
                      <span class="field-icon"><mat-icon fontSet="material-symbols-outlined">{{ typeIcon(f.type) }}</mat-icon></span>
                      <button type="button" class="field-title" (click)="toggle(f.uid)">
                        <strong>{{ f.label || ('catalogAdmin.untitled' | translate) }}@if (f.required) {<span class="req">*</span>}</strong>
                        <small>{{ 'catalogAdmin.type.' + f.type | translate }} · <code>{{ f.key }}</code></small>
                      </button>
                      <button mat-icon-button type="button" (click)="duplicate(i)" [attr.aria-label]="'catalogAdmin.duplicate' | translate">
                        <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>
                      </button>
                      <button mat-icon-button type="button" class="danger" (click)="remove(i)" [attr.aria-label]="'common.delete' | translate">
                        <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                      </button>
                    </div>

                    @if (expanded() === f.uid) {
                      <form class="field-body" (submit)="$event.preventDefault()">
                        <mat-form-field appearance="outline">
                          <mat-label>{{ 'catalogAdmin.label' | translate }}</mat-label>
                          <input matInput [ngModel]="f.label" (ngModelChange)="setLabel(f, $event)" name="label" maxlength="200" data-testid="field-label" />
                        </mat-form-field>
                        <mat-form-field appearance="outline">
                          <mat-label>{{ 'catalogAdmin.key' | translate }}</mat-label>
                          <input matInput [ngModel]="f.key" (ngModelChange)="setKey(f, $event)" name="key" maxlength="50" />
                          <mat-hint>{{ 'catalogAdmin.keyHint' | translate }}</mat-hint>
                        </mat-form-field>
                        <mat-form-field appearance="outline">
                          <mat-label>{{ 'catalogAdmin.fieldType' | translate }}</mat-label>
                          <mat-select [ngModel]="f.type" (ngModelChange)="setType(f, $event)" name="type">
                            @for (t of fieldTypes; track t.type) {
                              <mat-option [value]="t.type">{{ 'catalogAdmin.type.' + t.type | translate }}</mat-option>
                            }
                          </mat-select>
                        </mat-form-field>
                        <mat-form-field appearance="outline">
                          <mat-label>{{ 'catalogAdmin.help' | translate }}</mat-label>
                          <input matInput [ngModel]="f.helpText ?? ''" (ngModelChange)="patch(f, { helpText: $event || undefined })" name="help" maxlength="300" />
                        </mat-form-field>

                        @switch (f.type) {
                          @case ('Select') {
                            <div class="options wide">
                              <span class="sub-title">{{ 'catalogAdmin.options' | translate }}</span>
                              @for (o of f.options ?? []; track $index; let oi = $index) {
                                <div class="option-row">
                                  <input class="plain" [ngModel]="o.label" (ngModelChange)="setOption(f, oi, $event)" [name]="'opt' + oi" [placeholder]="'catalogAdmin.optionLabel' | translate" />
                                  <code>{{ o.value }}</code>
                                  <button mat-icon-button type="button" (click)="removeOption(f, oi)" [attr.aria-label]="'common.delete' | translate">
                                    <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
                                  </button>
                                </div>
                              }
                              <button mat-button type="button" (click)="addOption(f)" data-testid="add-option">
                                <mat-icon fontSet="material-symbols-outlined">add</mat-icon> {{ 'catalogAdmin.addOption' | translate }}
                              </button>
                            </div>
                          }
                          @case ('Number') {
                            <mat-form-field appearance="outline">
                              <mat-label>{{ 'catalogAdmin.min' | translate }}</mat-label>
                              <input matInput type="number" [ngModel]="f.min" (ngModelChange)="patch(f, { min: num($event) })" name="min" />
                            </mat-form-field>
                            <mat-form-field appearance="outline">
                              <mat-label>{{ 'catalogAdmin.max' | translate }}</mat-label>
                              <input matInput type="number" [ngModel]="f.max" (ngModelChange)="patch(f, { max: num($event) })" name="max" />
                            </mat-form-field>
                          }
                          @case ('File') {
                            <mat-form-field appearance="outline">
                              <mat-label>{{ 'catalogAdmin.maxFiles' | translate }}</mat-label>
                              <input matInput type="number" min="1" max="5" [ngModel]="f.maxFiles ?? 1" (ngModelChange)="patch(f, { maxFiles: num($event) })" name="maxFiles" />
                            </mat-form-field>
                          }
                          @case ('Date') {}
                          @default {
                            <mat-form-field appearance="outline">
                              <mat-label>{{ 'catalogAdmin.maxLength' | translate }}</mat-label>
                              <input matInput type="number" min="1" [ngModel]="f.maxLength" (ngModelChange)="patch(f, { maxLength: num($event) })" name="maxLength" />
                            </mat-form-field>
                            @if (f.type === 'Text') {
                              <mat-form-field appearance="outline">
                                <mat-label>{{ 'catalogAdmin.pattern' | translate }}</mat-label>
                                <input matInput [ngModel]="f.pattern ?? ''" (ngModelChange)="patch(f, { pattern: $event || undefined })" name="pattern" placeholder="\\d{4}-\\d{2}" />
                              </mat-form-field>
                            }
                          }
                        }
                        <mat-slide-toggle class="wide" [ngModel]="!!f.required" (ngModelChange)="patch(f, { required: $event })" name="required">
                          {{ 'catalogAdmin.required' | translate }}
                        </mat-slide-toggle>
                      </form>
                    }
                  </div>
                } @empty {
                  <p class="empty">{{ 'catalogAdmin.noFields' | translate }}</p>
                }
              </div>
            </div>

            @if (error(); as message) {
              <p class="error" role="alert" data-testid="type-error">
                <mat-icon fontSet="material-symbols-outlined">error</mat-icon>{{ message }}
              </p>
            }

            <div class="actions">
              @if (editingId(); as id) {
                <button mat-button type="button" (click)="toggleActive(id)">
                  {{ (selectedActive() ? 'catalogAdmin.retire' : 'catalogAdmin.restore') | translate }}
                </button>
              }
              <span class="spacer"></span>
              <button mat-button type="button" (click)="open.set(false)">{{ 'common.cancel' | translate }}</button>
              <button mat-flat-button type="button" (click)="save()" [disabled]="!name.trim() || saving()" data-testid="save-type">
                {{ 'common.save' | translate }}
              </button>
            </div>
          </div>

          <aside class="preview" [attr.aria-label]="'catalogAdmin.preview' | translate">
            <div class="preview-head">
              <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon> {{ 'catalogAdmin.preview' | translate }}
            </div>
            <div class="preview-card" data-testid="preview">
              <span class="preview-cat"><mat-icon fontSet="material-symbols-outlined">{{ icon(category) }}</mat-icon>{{ category | enumLabel: 'category' }}</span>
              <h3>{{ name || ('catalogAdmin.untitled' | translate) }}</h3>
              @if (description) {
                <p class="muted">{{ description }}</p>
              }
              @if (isConfidential) {
                <p class="confidential"><mat-icon fontSet="material-symbols-outlined">lock</mat-icon>{{ 'newRequest.confidential' | translate }}</p>
              }
              @if (previewGroup(); as group) {
                <form (submit)="$event.preventDefault()">
                  <app-dynamic-form [fields]="previewFields()" [group]="group" />
                </form>
              }
            </div>
          </aside>
        </section>
      }
    </div>
  `,
  styles: `
    .layout {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: 20px;
      align-items: start;
    }
    .layout.editing {
      grid-template-columns: 260px minmax(0, 1fr);
    }
    .types {
      display: grid;
      gap: 6px;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
    }
    .layout.editing .types {
      grid-template-columns: 1fr;
      position: sticky;
      top: 72px;
    }
    .type {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 10px 12px;
      border-radius: 12px;
      border: 1px solid var(--app-border);
      background: var(--app-card-bg);
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;
      transition: border-color 120ms, background 120ms;
    }
    .type:hover {
      border-color: color-mix(in srgb, var(--mat-sys-primary) 40%, transparent);
    }
    .type.selected {
      border-color: var(--mat-sys-primary);
      background: color-mix(in srgb, var(--mat-sys-primary) 7%, var(--app-card-bg));
    }
    .type.inactive .type-text {
      opacity: 0.55;
    }
    .type-icon,
    .field-icon {
      flex: none;
      width: 34px;
      height: 34px;
      border-radius: 10px;
      display: grid;
      place-items: center;
      color: var(--mat-sys-primary);
      background: color-mix(in srgb, var(--mat-sys-primary) 11%, transparent);
    }
    .type-icon mat-icon,
    .field-icon mat-icon {
      font-size: 19px;
      width: 19px;
      height: 19px;
    }
    .type-text {
      display: grid;
      flex: 1;
      min-width: 0;
    }
    .type-text strong {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    small {
      color: var(--app-muted);
      font-size: 0.78rem;
    }
    .flag {
      color: var(--mat-sys-error);
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .pill {
      padding: 1px 8px;
      border-radius: 10px;
      font-size: 0.72rem;
      font-weight: 600;
      white-space: nowrap;
    }
    .pill.off {
      color: var(--mat-sys-on-error-container);
      background: var(--mat-sys-error-container);
    }
    .editor {
      display: grid;
      grid-template-columns: minmax(0, 1.15fr) minmax(300px, 0.85fr);
      gap: 20px;
      align-items: start;
    }
    .builder {
      display: grid;
      gap: 16px;
    }
    .card {
      padding: 18px 20px;
      border-radius: var(--app-radius);
      border: 1px solid var(--app-border);
      background: var(--app-card-bg);
      box-shadow: var(--app-shadow);
    }
    h2 {
      font: 600 1.05rem Inter, 'IBM Plex Sans Arabic', sans-serif;
      margin: 0 0 4px;
    }
    .settings {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 4px 12px;
    }
    .wide {
      grid-column: 1 / -1;
    }
    .toggles {
      display: grid;
      gap: 12px;
      margin-top: 4px;
    }
    .toggle-text {
      display: grid;
      margin-inline-start: 8px;
    }
    .fields-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 12px;
    }
    .count {
      margin-inline-start: 6px;
      padding: 1px 8px;
      border-radius: 10px;
      font-size: 0.78rem;
      background: var(--mat-sys-surface-container-high);
    }
    .fields {
      display: grid;
      gap: 8px;
    }
    .field {
      border-radius: 12px;
      border: 1px solid var(--app-border);
      background: var(--app-card-bg);
    }
    .field.open {
      border-color: color-mix(in srgb, var(--mat-sys-primary) 50%, transparent);
      box-shadow: 0 0 0 3px var(--app-ring);
    }
    .field-head {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 6px 8px 8px;
    }
    .handle {
      color: var(--app-muted);
      cursor: grab;
    }
    .field-title {
      display: grid;
      flex: 1;
      min-width: 0;
      border: 0;
      padding: 0;
      background: none;
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .req {
      color: var(--mat-sys-error);
      margin-inline-start: 2px;
    }
    .field-body {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 14px 12px;
      padding: 4px 14px 14px;
      border-top: 1px solid var(--app-border);
    }
    .options {
      display: grid;
      gap: 6px;
    }
    .sub-title {
      font-size: 0.85rem;
      font-weight: 600;
    }
    .option-row {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .plain {
      flex: 1;
      height: 38px;
      padding: 0 12px;
      border-radius: 10px;
      border: 1px solid var(--app-field-border);
      background: var(--app-field-bg);
      color: inherit;
      font: inherit;
      outline: none;
    }
    .plain:focus {
      border-color: var(--mat-sys-primary);
      box-shadow: 0 0 0 3px var(--app-ring);
    }
    code {
      font-size: 0.78rem;
      color: var(--app-muted);
    }
    .danger {
      color: var(--mat-sys-error);
    }
    .empty {
      color: var(--app-muted);
      text-align: center;
      padding: 16px;
    }
    .error {
      display: flex;
      gap: 8px;
      align-items: flex-start;
      padding: 10px 12px;
      border-radius: 12px;
      margin: 0;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .actions {
      display: flex;
      gap: 8px;
      align-items: center;
    }
    .spacer {
      flex: 1;
    }
    .preview {
      position: sticky;
      top: 72px;
    }
    .preview-head {
      display: flex;
      align-items: center;
      gap: 6px;
      margin-bottom: 8px;
      font-size: 0.8rem;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--app-muted);
    }
    .preview-head mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .preview-card {
      padding: 20px;
      border-radius: var(--app-radius);
      border: 1px dashed color-mix(in srgb, var(--mat-sys-primary) 40%, transparent);
      background: var(--app-card-bg);
      max-height: calc(100vh - 140px);
      overflow: auto;
    }
    .preview-cat {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 0.8rem;
      font-weight: 600;
      color: var(--mat-sys-primary);
    }
    .preview-cat mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    h3 {
      font: 700 1.25rem Inter, 'IBM Plex Sans Arabic', sans-serif;
      margin: 6px 0 4px;
    }
    .muted {
      color: var(--app-muted);
      margin-top: 0;
    }
    .confidential {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 12px;
      border-radius: 10px;
      font-size: 0.85rem;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .cdk-drag-preview {
      box-shadow: var(--app-overlay-shadow);
      border-radius: 12px;
    }
    .cdk-drag-placeholder {
      opacity: 0.35;
    }
    .cdk-drag-animating,
    .fields.cdk-drop-list-dragging .field:not(.cdk-drag-placeholder) {
      transition: transform 200ms cubic-bezier(0, 0, 0.2, 1);
    }
    @media (max-width: 1279px) {
      .editor {
        grid-template-columns: 1fr;
      }
      .preview {
        position: static;
      }
    }
    @media (max-width: 959px) {
      .layout.editing {
        grid-template-columns: 1fr;
      }
      .layout.editing .types {
        position: static;
      }
    }
  `,
})
export class CatalogAdmin {
  private readonly api = inject(CatalogApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  protected readonly categories = CATEGORIES;
  protected readonly priorities = PRIORITIES;
  protected readonly fieldTypes = FIELD_TYPES;
  protected readonly types = rxResource({ stream: () => this.api.listAll() });

  protected readonly open = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly fields = signal<DraftField[]>([]);
  protected readonly expanded = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly selectedActive = computed(() => this.types.value()?.find((t) => t.id === this.editingId())?.isActive ?? true);

  protected name = '';
  protected description = '';
  protected category: string = 'Payroll';
  protected priority: string = 'Medium';
  protected isConfidential = false;
  protected isSensitive = false;
  private nextUid = 1;

  /** The fields as the employee will see them (form group rebuilt on every change). */
  protected readonly previewFields = computed(() => this.fields().map((f) => this.clean(f)));
  protected readonly previewGroup = computed(() => {
    try {
      return buildFormGroup(this.previewFields());
    } catch {
      return null;
    }
  });

  protected icon(category: string): string {
    return categoryIcon(category);
  }

  protected typeIcon(type: FieldType): string {
    return FIELD_TYPES.find((t) => t.type === type)?.icon ?? 'short_text';
  }

  protected startNew(): void {
    this.editingId.set(null);
    this.name = '';
    this.description = '';
    this.category = 'Payroll';
    this.priority = 'Medium';
    this.isConfidential = false;
    this.isSensitive = false;
    this.fields.set([]);
    this.expanded.set(null);
    this.error.set(null);
    this.open.set(true);
    this.addField('Text');
  }

  protected edit(summary: RequestTypeSummary): void {
    this.api.get(summary.id).subscribe((type) => {
      this.editingId.set(type.id);
      this.name = type.name;
      this.description = type.description;
      this.category = type.category;
      this.priority = type.defaultPriority;
      this.isConfidential = type.isConfidential;
      this.isSensitive = !!type.isSensitive;
      this.fields.set(type.fields.map((f) => ({ ...f, uid: this.nextUid++, keyTouched: true })));
      this.expanded.set(null);
      this.error.set(null);
      this.open.set(true);
    });
  }

  protected addField(type: FieldType): void {
    const label = this.translate.instant(`catalogAdmin.type.${type}`) as string;
    const field: DraftField = {
      uid: this.nextUid++,
      keyTouched: false,
      key: this.uniqueKey(keyFromLabel(label)),
      label,
      type,
      required: false,
      ...(type === 'Select' ? { options: [{ value: 'option_1', label: 'Option 1' }] } : {}),
      ...(type === 'File' ? { maxFiles: 1 } : {}),
    };
    this.fields.update((list) => [...list, field]);
    this.expanded.set(field.uid);
  }

  protected toggle(uid: number): void {
    this.expanded.update((current) => (current === uid ? null : uid));
  }

  protected patch(field: DraftField, change: Partial<DraftField>): void {
    this.fields.update((list) => list.map((f) => (f.uid === field.uid ? { ...f, ...change } : f)));
  }

  protected setLabel(field: DraftField, label: string): void {
    this.patch(field, field.keyTouched ? { label } : { label, key: this.uniqueKey(keyFromLabel(label || 'field'), field.uid) });
  }

  protected setKey(field: DraftField, key: string): void {
    this.patch(field, { key: key.replace(/[^A-Za-z0-9_]/g, ''), keyTouched: true });
  }

  protected setType(field: DraftField, type: FieldType): void {
    this.patch(field, {
      type,
      options: type === 'Select' ? (field.options?.length ? field.options : [{ value: 'option_1', label: 'Option 1' }]) : undefined,
      maxFiles: type === 'File' ? (field.maxFiles ?? 1) : undefined,
      min: type === 'Number' ? field.min : undefined,
      max: type === 'Number' ? field.max : undefined,
      maxLength: type === 'Text' || type === 'Textarea' ? field.maxLength : undefined,
      pattern: type === 'Text' ? field.pattern : undefined,
    });
  }

  protected addOption(field: DraftField): void {
    const options = field.options ?? [];
    const label = `Option ${options.length + 1}`;
    this.patch(field, { options: [...options, { value: this.optionValue(label, options.map((o) => o.value)), label }] });
  }

  /** The stored value follows the label (snake_case) and stays unique. */
  protected setOption(field: DraftField, index: number, label: string): void {
    const options = [...(field.options ?? [])];
    const others = options.filter((_, i) => i !== index).map((o) => o.value);
    options[index] = { label, value: this.optionValue(label || `option ${index + 1}`, others) };
    this.patch(field, { options });
  }

  protected removeOption(field: DraftField, index: number): void {
    this.patch(field, { options: (field.options ?? []).filter((_, i) => i !== index) });
  }

  protected duplicate(index: number): void {
    const source = this.fields()[index];
    const copy: DraftField = { ...source, uid: this.nextUid++, key: this.uniqueKey(`${source.key}Copy`), label: `${source.label} (copy)` };
    this.fields.update((list) => [...list.slice(0, index + 1), copy, ...list.slice(index + 1)]);
    this.expanded.set(copy.uid);
  }

  protected remove(index: number): void {
    this.fields.update((list) => list.filter((_, i) => i !== index));
  }

  protected drop(event: CdkDragDrop<DraftField[]>): void {
    const list = [...this.fields()];
    moveItemInArray(list, event.previousIndex, event.currentIndex);
    this.fields.set(list);
  }

  protected num(value: unknown): number | undefined {
    return value === null || value === '' || value === undefined || Number.isNaN(Number(value)) ? undefined : Number(value);
  }

  protected save(): void {
    this.saving.set(true);
    this.error.set(null);
    const body = {
      name: this.name.trim(),
      description: this.description.trim(),
      category: this.category,
      isConfidential: this.isConfidential,
      isSensitive: this.isSensitive,
      defaultPriority: this.priority,
      fields: this.fields().map((f) => this.clean(f)),
    };
    this.api.save(this.editingId(), body).subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.editingId.set(saved.id);
        this.snackBar.open(this.translate.instant('common.saved') as string, undefined, { duration: 3000 });
        this.types.reload();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(problemMessage(this.translate, error));
      },
    });
  }

  protected toggleActive(id: string): void {
    this.api.setActive(id, !this.selectedActive()).subscribe(() => this.types.reload());
  }

  /** Only the properties that matter for the field's type, without editor bookkeeping. */
  private clean(f: DraftField): FormFieldDef {
    const field: FormFieldDef = { key: f.key, label: f.label, type: f.type, required: !!f.required };
    if (f.helpText) field.helpText = f.helpText;
    if (f.type === 'Select') field.options = f.options ?? [];
    if (f.type === 'Number') {
      if (f.min !== undefined) field.min = f.min;
      if (f.max !== undefined) field.max = f.max;
    }
    if ((f.type === 'Text' || f.type === 'Textarea') && f.maxLength) field.maxLength = f.maxLength;
    if (f.type === 'Text' && f.pattern) field.pattern = f.pattern;
    if (f.type === 'File') field.maxFiles = f.maxFiles ?? 1;
    return field;
  }

  private uniqueKey(base: string, exceptUid?: number): string {
    const taken = new Set(this.fields().filter((f) => f.uid !== exceptUid).map((f) => f.key));
    let key = base;
    for (let i = 2; taken.has(key); i++) key = `${base}${i}`;
    return key;
  }

  private optionValue(label: string, taken: string[]): string {
    const base =
      label
        .normalize('NFD')
        .replace(/\p{Diacritic}/gu, '')
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '_')
        .replace(/^_+|_+$/g, '') || 'option';
    let value = base;
    for (let i = 2; taken.includes(value); i++) value = `${base}_${i}`;
    return value;
  }
}
