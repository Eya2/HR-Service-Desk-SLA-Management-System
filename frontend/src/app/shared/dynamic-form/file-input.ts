import { Component, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { fileSize } from '../ui/labels';
import { ACCEPTED_EXTENSIONS } from './form-builder';
import { TranslatePipe } from '@ngx-translate/core';

/** A file picker bound to a form control holding File[]. */
@Component({
  selector: 'app-file-input',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => FileInput), multi: true }],
  template: `
    <input
      #picker
      type="file"
      hidden
      [accept]="accept"
      [multiple]="maxFiles() > 1"
      (change)="add(picker.files); picker.value = ''"
    />
    <button mat-stroked-button type="button" (click)="picker.click()" [disabled]="disabled() || files().length >= maxFiles()">
      <mat-icon fontSet="material-symbols-outlined">attach_file</mat-icon>
      {{ (maxFiles() > 1 ? 'form.addFiles' : 'form.chooseFile') | translate }}
    </button>
    <ul class="files">
      @for (file of files(); track $index) {
        <li>
          <mat-icon fontSet="material-symbols-outlined">description</mat-icon>
          <span class="name">{{ file.name }}</span>
          <span class="size">{{ size(file.size) }}</span>
          <button mat-icon-button type="button" (click)="remove($index)" [attr.aria-label]="'form.removeFile' | translate: { name: file.name }">
            <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
          </button>
        </li>
      }
    </ul>
  `,
  styles: `
    .files {
      list-style: none;
      padding: 0;
      margin: 8px 0 0;
    }
    li {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .name {
      flex: 1;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .size {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class FileInput implements ControlValueAccessor {
  readonly maxFiles = input(1);

  protected readonly accept = ACCEPTED_EXTENSIONS.join(',');
  protected readonly files = signal<File[]>([]);
  protected readonly disabled = signal(false);
  protected readonly size = fileSize;

  private onChange: (files: File[]) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  writeValue(value: File[] | null): void {
    this.files.set(value ?? []);
  }

  registerOnChange(fn: (files: File[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
  }

  protected add(list: FileList | null): void {
    if (!list || list.length === 0) return;
    this.update([...this.files(), ...Array.from(list)]);
  }

  protected remove(index: number): void {
    this.update(this.files().filter((_, i) => i !== index));
  }

  private update(files: File[]): void {
    this.files.set(files);
    this.onChange(files);
    this.onTouched();
  }
}
