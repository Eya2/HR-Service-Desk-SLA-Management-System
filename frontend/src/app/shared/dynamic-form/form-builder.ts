import { AbstractControl, FormControl, FormGroup, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { FormFieldDef } from '../../core/api/api.models';

/** Mirrors the API's attachment policy so users get feedback before uploading. */
export const ACCEPTED_EXTENSIONS = ['.pdf', '.png', '.jpg', '.jpeg', '.docx', '.xlsx'];
export const MAX_FILE_BYTES = 10 * 1024 * 1024;

const DEFAULT_MAX_LENGTH: Partial<Record<FormFieldDef['type'], number>> = { Text: 500, Textarea: 4000 };

export type DynamicFormGroup = FormGroup<Record<string, FormControl<unknown>>>;

/** Required for file lists: at least one file. */
const requiredFiles: ValidatorFn = (control) =>
  Array.isArray(control.value) && control.value.length > 0 ? null : { required: true };

function maxFiles(max: number): ValidatorFn {
  return (control) => (Array.isArray(control.value) && control.value.length > max ? { maxFiles: { max } } : null);
}

/** Rejects files the API would refuse (type by extension here; the server also checks the content). */
const acceptedFiles: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const files = (control.value ?? []) as File[];
  const badType = files.find((f) => !ACCEPTED_EXTENSIONS.some((ext) => f.name.toLowerCase().endsWith(ext)));
  if (badType) return { fileType: { name: badType.name } };
  const tooBig = files.find((f) => f.size > MAX_FILE_BYTES);
  if (tooBig) return { fileSize: { name: tooBig.name } };
  return null;
};

export function validatorsFor(field: FormFieldDef): ValidatorFn[] {
  if (field.type === 'File') {
    return [...(field.required ? [requiredFiles] : []), maxFiles(field.maxFiles ?? 1), acceptedFiles];
  }

  const validators: ValidatorFn[] = field.required ? [Validators.required] : [];
  if (field.type === 'Number') {
    if (field.min != null) validators.push(Validators.min(field.min));
    if (field.max != null) validators.push(Validators.max(field.max));
  }
  if (field.type === 'Text' || field.type === 'Textarea') {
    if (field.minLength != null) validators.push(Validators.minLength(field.minLength));
    validators.push(Validators.maxLength(field.maxLength ?? DEFAULT_MAX_LENGTH[field.type]!));
    // Same full-match semantics as the API.
    if (field.pattern) validators.push(Validators.pattern(`^(?:${field.pattern})$`));
  }
  return validators;
}

/** Builds one control per field: strings for text, numbers, dates (YYYY-MM-DD) and selects; File[] for files. */
export function buildFormGroup(fields: readonly FormFieldDef[]): DynamicFormGroup {
  const controls: Record<string, FormControl<unknown>> = {};
  for (const field of fields) {
    const initial = field.type === 'File' ? [] : field.type === 'Number' ? null : '';
    controls[field.key] = new FormControl<unknown>(initial, validatorsFor(field));
  }
  return new FormGroup(controls);
}

/** Splits the form value into JSON answers (empty values dropped) and files per field. */
export function toSubmission(
  fields: readonly FormFieldDef[],
  group: DynamicFormGroup,
): { values: Record<string, unknown>; files: Record<string, File[]> } {
  const values: Record<string, unknown> = {};
  const files: Record<string, File[]> = {};
  for (const field of fields) {
    const value = group.controls[field.key].value;
    if (field.type === 'File') {
      if (Array.isArray(value) && value.length > 0) files[field.key] = value as File[];
    } else if (value !== null && value !== undefined && `${value}`.trim() !== '') {
      // The input type is bound dynamically, so Angular's number accessor does not apply: numbers arrive as text.
      values[field.key] = field.type === 'Number' ? Number(value) : typeof value === 'string' ? value.trim() : value;
    }
  }
  return { values, files };
}

/** Translates a key ("form.required") with parameters. */
export type Translate = (key: string, params?: Record<string, unknown>) => string;

/** First error message for a control, worded for employees. */
export function errorMessage(field: FormFieldDef, control: AbstractControl, t: Translate): string | null {
  const errors = control.errors;
  if (!errors) return null;
  if (errors['server']) return errors['server'] as string;
  if (errors['required']) return t('form.required', { label: field.label });
  if (errors['min']) return t('form.min', { min: field.min });
  if (errors['max']) return t('form.max', { max: field.max });
  if (errors['minlength']) return t('form.minLength', { n: field.minLength });
  if (errors['maxlength']) return t('form.maxLength', { n: errors['maxlength'].requiredLength });
  if (errors['pattern']) return field.helpText ? `${t('form.pattern')} ${field.helpText}` : t('form.pattern');
  if (errors['maxFiles']) return t('form.maxFiles', { n: errors['maxFiles'].max });
  if (errors['fileType']) return t('form.fileType', { name: errors['fileType'].name, types: ACCEPTED_EXTENSIONS.join(', ') });
  if (errors['fileSize']) return t('form.fileSize', { name: errors['fileSize'].name });
  return t('form.invalid');
}
