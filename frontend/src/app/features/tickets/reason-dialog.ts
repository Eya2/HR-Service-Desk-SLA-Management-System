import { Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { StatusAction } from './status-actions';

/** Asks for the message that accompanies a status change. Closes with the text, or undefined if cancelled. */
@Component({
  selector: 'app-reason-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>{{ action.label }}</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="field">
        <mat-label>{{ action.reasonLabel }}</mat-label>
        <textarea matInput rows="4" maxlength="4000" [formControl]="reason" data-testid="reason"></textarea>
        @if (reason.hasError('required')) {
          <mat-error>A message is required.</mat-error>
        }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancel</button>
      <button mat-flat-button type="button" [disabled]="reason.invalid" (click)="confirm()" data-testid="confirm-status">
        {{ action.label }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .field {
      width: 100%;
      min-width: 320px;
    }
  `,
})
export class ReasonDialog {
  protected readonly action = inject<StatusAction>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ReasonDialog, string>);

  protected readonly reason = new FormControl('', {
    nonNullable: true,
    validators: this.action.reason === 'required' ? [Validators.required, Validators.maxLength(4000)] : [Validators.maxLength(4000)],
  });

  protected confirm(): void {
    this.ref.close(this.reason.value.trim());
  }
}
