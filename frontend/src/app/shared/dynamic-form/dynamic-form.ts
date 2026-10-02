import { Component, inject, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { FormFieldDef } from '../../core/api/api.models';
import { FileInput } from './file-input';
import { DynamicFormGroup, errorMessage } from './form-builder';
import { TranslateService } from '@ngx-translate/core';

/** Renders a request type's fields into an existing form group (built with buildFormGroup). */
@Component({
  selector: 'app-dynamic-form',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, FileInput],
  template: `
    <div [formGroup]="group()" class="fields">
      @for (field of fields(); track field.key) {
        @let control = group().controls[field.key];
        @switch (field.type) {
          @case ('File') {
            <div class="file-field" [attr.data-field]="field.key">
              <label class="file-label">{{ field.label }}{{ field.required ? ' *' : '' }}</label>
              @if (field.helpText) {
                <p class="hint">{{ field.helpText }}</p>
              }
              <app-file-input [formControlName]="field.key" [maxFiles]="field.maxFiles ?? 1" />
              @if (control.invalid && control.touched) {
                <p class="error" role="alert">{{ message(field) }}</p>
              }
            </div>
          }
          @case ('Select') {
            <mat-form-field appearance="outline" [attr.data-field]="field.key">
              <mat-label>{{ field.label }}</mat-label>
              <mat-select [formControlName]="field.key" [required]="!!field.required">
                @if (!field.required) {
                  <mat-option [value]="''">—</mat-option>
                }
                @for (option of field.options ?? []; track option.value) {
                  <mat-option [value]="option.value">{{ option.label }}</mat-option>
                }
              </mat-select>
              @if (field.helpText) {
                <mat-hint>{{ field.helpText }}</mat-hint>
              }
              <mat-error>{{ message(field) }}</mat-error>
            </mat-form-field>
          }
          @case ('Textarea') {
            <mat-form-field appearance="outline" [attr.data-field]="field.key">
              <mat-label>{{ field.label }}</mat-label>
              <textarea matInput rows="4" [formControlName]="field.key" [required]="!!field.required"></textarea>
              @if (field.helpText) {
                <mat-hint>{{ field.helpText }}</mat-hint>
              }
              <mat-error>{{ message(field) }}</mat-error>
            </mat-form-field>
          }
          @default {
            <mat-form-field appearance="outline" [attr.data-field]="field.key">
              <mat-label>{{ field.label }}</mat-label>
              <input
                matInput
                [type]="field.type === 'Number' ? 'number' : field.type === 'Date' ? 'date' : 'text'"
                [formControlName]="field.key"
                [required]="!!field.required"
                [attr.min]="field.min ?? null"
                [attr.max]="field.max ?? null"
              />
              @if (field.helpText) {
                <mat-hint>{{ field.helpText }}</mat-hint>
              }
              <mat-error>{{ message(field) }}</mat-error>
            </mat-form-field>
          }
        }
      }
    </div>
  `,
  styles: `
    .fields {
      display: grid;
      gap: 8px;
    }
    .file-field {
      margin-bottom: 16px;
    }
    .file-label {
      display: block;
      font: var(--mat-sys-body-large);
      margin-bottom: 4px;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
      margin: 0 0 8px;
    }
    .error {
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-small);
      margin: 4px 0 0;
    }
  `,
})
export class DynamicForm {
  private readonly translate = inject(TranslateService);

  readonly fields = input.required<readonly FormFieldDef[]>();
  readonly group = input.required<DynamicFormGroup>();

  protected message(field: FormFieldDef): string | null {
    return errorMessage(field, this.group().controls[field.key], (key, params) => this.translate.instant(key, params) as string);
  }
}
