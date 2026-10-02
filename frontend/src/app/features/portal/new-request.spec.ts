import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { payslipType } from '../../testing/catalog-fixtures';
import { NewRequest } from './new-request';

describe('NewRequest', () => {
  let fixture: ComponentFixture<NewRequest>;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [NewRequest],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(NewRequest);
    fixture.componentRef.setInput('typeId', payslipType.id);
    fixture.detectChanges();
    http.expectOne(`/api/request-types/${payslipType.id}`).flush(payslipType);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const component = () => fixture.componentInstance as unknown as {
    answers: () => import('../../shared/dynamic-form/form-builder').DynamicFormGroup;
  };

  async function submit(): Promise<void> {
    el().querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  function fillValidAnswers(): void {
    component().answers().patchValue({
      payPeriod: '2026-03',
      issue: 'missing_overtime',
      payslip: [new File(['%PDF'], 'payslip.pdf')],
    });
  }

  it('prefills the title with the request type name', () => {
    expect((el().querySelector('input[formcontrolname="title"]') as HTMLInputElement).value).toBe('Payslip correction');
  });

  it('does not submit an incomplete form', async () => {
    await submit();

    http.expectNone('/api/tickets');
    expect(el().textContent).toContain('Please correct the highlighted fields.');
  });

  it('submits and opens the new case', async () => {
    fillValidAnswers();
    await submit();

    http.expectOne('/api/tickets').flush({ id: 't-9', reference: 'HR-2026-000009' });

    expect(router.navigate).toHaveBeenCalledWith(['/tickets', 't-9']);
  });

  it('places server validation messages on the matching fields', async () => {
    fillValidAnswers();
    await submit();

    http.expectOne('/api/tickets').flush(
      { title: 'Validation', errors: { 'values.payslip': ["'payslip.pdf' content does not match its extension."] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await fixture.whenStable();

    expect(component().answers().controls['payslip'].errors).toEqual({ server: "'payslip.pdf' content does not match its extension." });
    expect(el().querySelector('[data-field="payslip"] .error')?.textContent).toContain('does not match');
  });

  it('suggests help articles matching the title while it is typed', async () => {
    const title = el().querySelector('input[formcontrolname="title"]') as HTMLInputElement;
    title.value = 'payslip overtime missing';
    title.dispatchEvent(new Event('input'));
    await new Promise((resolve) => setTimeout(resolve, 400)); // past the typing pause
    TestBed.tick();

    const req = http.expectOne((r) => r.url === '/api/knowledge/suggest');
    expect(req.request.params.get('text')).toBe('payslip overtime missing');
    req.flush([
      { id: 'k1', title: 'How do I read my payslip?', summary: 'Each line explained.', category: 'Payroll', isPublished: true, viewCount: 3, helpfulCount: 1 },
    ]);
    await fixture.whenStable();

    const box = el().querySelector('[data-testid="suggestions"]');
    expect(box?.textContent).toContain('These articles may answer your question');
    expect(box?.querySelector('a')?.getAttribute('href')).toBe('/portal/help/k1');
  });
});
