import { payslipType } from '../../testing/catalog-fixtures';
import { buildFormGroup, errorMessage, toSubmission } from './form-builder';

describe('dynamic form builder', () => {
  const fields = payslipType.fields;
  const field = (key: string) => fields.find((f) => f.key === key)!;
  const pdf = (name = 'payslip.pdf', size = 10) => new File([new Uint8Array(size)], name, { type: 'application/pdf' });

  it('creates one control per field with type-appropriate initial values', () => {
    const group = buildFormGroup(fields);

    expect(Object.keys(group.controls)).toEqual(['payPeriod', 'issue', 'expectedAmount', 'neededBy', 'notes', 'payslip']);
    expect(group.controls['payslip'].value).toEqual([]);
    expect(group.controls['expectedAmount'].value).toBeNull();
    expect(group.invalid).toBeTrue();
  });

  it('applies the pattern as a full match, like the API', () => {
    const control = buildFormGroup(fields).controls['payPeriod'];

    control.setValue('2026-03');
    expect(control.valid).toBeTrue();
    control.setValue('x2026-03');
    expect(control.hasError('pattern')).toBeTrue();
    expect(errorMessage(field('payPeriod'), control)).toBe('Invalid format. YYYY-MM');
  });

  it('enforces number bounds and text length', () => {
    const group = buildFormGroup(fields);

    group.controls['expectedAmount'].setValue(-1);
    expect(errorMessage(field('expectedAmount'), group.controls['expectedAmount'])).toBe('Minimum 0.');
    group.controls['notes'].setValue('x'.repeat(21));
    expect(errorMessage(field('notes'), group.controls['notes'])).toBe('At most 20 characters.');
  });

  it('validates files: required, count, type and size', () => {
    const control = buildFormGroup(fields).controls['payslip'];

    expect(errorMessage(field('payslip'), control)).toBe('Payslip is required.');
    control.setValue([pdf(), pdf('second.pdf')]);
    expect(control.hasError('maxFiles')).toBeTrue();
    control.setValue([pdf('virus.exe')]);
    expect(errorMessage(field('payslip'), control)).toContain('accepted types');
    control.setValue([pdf('huge.pdf', 11 * 1024 * 1024)]);
    expect(errorMessage(field('payslip'), control)).toBe('huge.pdf is larger than 10 MB.');
    control.setValue([pdf()]);
    expect(control.valid).toBeTrue();
  });

  it('splits answers from files and drops empty values', () => {
    const group = buildFormGroup(fields);
    const file = pdf();
    group.patchValue({ payPeriod: ' 2026-03 ', issue: 'other', expectedAmount: 320.5, neededBy: '', notes: '  ', payslip: [file] });

    const submission = toSubmission(fields, group);

    expect(submission.values).toEqual({ payPeriod: '2026-03', issue: 'other', expectedAmount: 320.5 });
    expect(submission.files).toEqual({ payslip: [file] });
  });

  it('sends numbers typed into number inputs as JSON numbers', () => {
    const group = buildFormGroup(fields);
    group.patchValue({ expectedAmount: '320.5' });

    expect(toSubmission(fields, group).values['expectedAmount']).toBe(320.5);
  });

  it('shows server messages first', () => {
    const control = buildFormGroup(fields).controls['issue'];
    control.setErrors({ server: 'Issue must be one of the proposed options.' });

    expect(errorMessage(field('issue'), control)).toBe('Issue must be one of the proposed options.');
  });
});
