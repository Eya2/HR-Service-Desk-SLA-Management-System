import { TestBed } from '@angular/core/testing';
import { payslipType } from '../../testing/catalog-fixtures';
import { DynamicForm } from './dynamic-form';
import { buildFormGroup } from './form-builder';

describe('DynamicForm', () => {
  async function render() {
    const fixture = TestBed.createComponent(DynamicForm);
    const group = buildFormGroup(payslipType.fields);
    fixture.componentRef.setInput('fields', payslipType.fields);
    fixture.componentRef.setInput('group', group);
    await fixture.whenStable();
    return { el: fixture.nativeElement as HTMLElement, group, fixture };
  }

  it('renders the right input for each field type', async () => {
    const { el } = await render();
    const inputType = (key: string) => el.querySelector(`[data-field="${key}"] input`)?.getAttribute('type');

    expect(inputType('payPeriod')).toBe('text');
    expect(inputType('expectedAmount')).toBe('number');
    expect(inputType('neededBy')).toBe('date');
    expect(el.querySelector('[data-field="issue"] mat-select')).not.toBeNull();
    expect(el.querySelector('[data-field="notes"] textarea')).not.toBeNull();
    expect(el.querySelector('[data-field="payslip"] app-file-input')).not.toBeNull();
    expect(el.textContent).toContain('YYYY-MM');
  });

  it('binds typed values to the form group', async () => {
    const { el, group } = await render();
    const input = el.querySelector('[data-field="payPeriod"] input') as HTMLInputElement;

    input.value = '2026-03';
    input.dispatchEvent(new Event('input'));

    expect(group.controls['payPeriod'].value).toBe('2026-03');
  });

  it('shows file errors once touched', async () => {
    const { el, group, fixture } = await render();

    group.markAllAsTouched();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(el.querySelector('[data-field="payslip"] .error')?.textContent).toContain('Payslip is required.');
  });
});
