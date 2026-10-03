import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Classification } from '../../core/api/api.models';
import { RequestPrefillStore } from '../../core/api/assistant.api';
import { catalog } from '../../testing/catalog-fixtures';
import { Catalog } from './catalog';

describe('Catalog', () => {
  async function render() {
    TestBed.configureTestingModule({
      imports: [Catalog],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(Catalog);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/request-types').flush(catalog);
    await fixture.whenStable();
    return fixture;
  }

  const names = (el: HTMLElement) => Array.from(el.querySelectorAll('.tile h2')).map((h) => h.textContent?.trim());

  it('lists every request type with a confidential marker', async () => {
    const el = (await render()).nativeElement as HTMLElement;

    expect(names(el)).toEqual(['Payslip correction', 'Work certificate', 'Harassment report']);
    expect(el.querySelector('[data-testid="type-Harassment report"]')?.textContent).toContain('Confidential');
  });

  it('filters as the user types, ignoring case and accents', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const search = el.querySelector('[data-testid="catalog-search"]') as HTMLInputElement;

    search.value = 'CERTIFICÁT';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(names(el)).toEqual(['Work certificate']);
  });

  describe('assistant', () => {
    const classification: Classification = {
      suggestion: {
        requestTypeId: 'rt-payslip',
        name: 'Payslip correction',
        category: 'Payroll',
        isConfidential: false,
        confidence: 0.9,
        title: 'Missing March overtime',
        values: { issue: 'missing_overtime', payPeriod: '2026-03' },
        reason: 'Overtime is missing from a payslip.',
      },
      alternatives: [{ id: 'rt-certificate', name: 'Work certificate', category: 'Certificates' }],
      articles: [{ id: 'a-1', title: 'Reading your payslip', summary: '', category: 'Payroll', isPublished: true, viewCount: 0, helpfulCount: 0 }],
      source: 'claude',
    };

    async function ask(fixture: Awaited<ReturnType<typeof render>>, text: string) {
      const el = fixture.nativeElement as HTMLElement;
      const input = el.querySelector('[data-testid="assistant-input"]') as HTMLTextAreaElement;
      input.value = text;
      input.dispatchEvent(new Event('input'));
      await fixture.whenStable();
      (el.querySelector('[data-testid="assistant-ask"]') as HTMLButtonElement).click();
      return TestBed.inject(HttpTestingController).expectOne('/api/assistant/classify');
    }

    it('waits for a few words before asking', async () => {
      const fixture = await render();
      const button = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="assistant-ask"]') as HTMLButtonElement;

      expect(button.disabled).toBeTrue();
    });

    it('shows the suggested request, its articles, and continues with the prepared answers', async () => {
      const fixture = await render();
      const router = TestBed.inject(Router);
      spyOn(router, 'navigate').and.resolveTo(true);
      const req = await ask(fixture, 'My March overtime is missing');
      expect(req.request.body).toEqual({ text: 'My March overtime is missing' });
      req.flush(classification);
      await fixture.whenStable();
      const el = fixture.nativeElement as HTMLElement;

      expect(el.querySelector('[data-testid="assistant-type"]')?.textContent).toContain('Payslip correction');
      expect(el.querySelector('[data-testid="assistant-match"]')?.textContent).toContain('Strong match');
      expect(el.querySelector('[data-testid="assistant-result"]')?.textContent).toContain('2 answer(s) pre-filled');
      expect(el.querySelector('[data-testid="assistant-result"]')?.textContent).toContain('Reading your payslip');

      (el.querySelector('[data-testid="assistant-continue"]') as HTMLButtonElement).click();

      expect(router.navigate).toHaveBeenCalledWith(['/portal/new', 'rt-payslip']);
      expect(TestBed.inject(RequestPrefillStore).take('rt-payslip')).toEqual({
        requestTypeId: 'rt-payslip',
        title: 'Missing March overtime',
        description: 'My March overtime is missing',
        values: { issue: 'missing_overtime', payPeriod: '2026-03' },
      });
    });

    it('says so when nothing matches', async () => {
      const fixture = await render();
      (await ask(fixture, 'zzz qqq xyzzy')).flush({ suggestion: null, alternatives: [], articles: [], source: 'local' });
      await fixture.whenStable();

      expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="assistant-none"]')).not.toBeNull();
    });
  });
});
