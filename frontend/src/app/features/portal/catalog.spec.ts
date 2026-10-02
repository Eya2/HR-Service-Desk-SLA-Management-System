import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
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

  const names = (el: HTMLElement) => Array.from(el.querySelectorAll('h2')).map((h) => h.textContent?.trim());

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
});
