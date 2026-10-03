import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CatalogAdmin, keyFromLabel } from './catalog-admin';

describe('keyFromLabel', () => {
  it('turns a label into a camelCase key that starts with a letter', () => {
    expect(keyFromLabel('Licence plate number')).toBe('licencePlateNumber');
    expect(keyFromLabel('Date de début')).toBe('dateDeDebut');
    expect(keyFromLabel('2nd phone')).toBe('field2ndPhone');
  });
});

describe('CatalogAdmin', () => {
  let fixture: ComponentFixture<CatalogAdmin>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ imports: [CatalogAdmin], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CatalogAdmin);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/request-types' && r.params.get('includeInactive') === 'true').flush([
      { id: 't1', name: 'Work certificate', description: '', category: 'Certificates', isConfidential: false, isActive: true, defaultPriority: 'Low' },
      { id: 't2', name: 'Old form', description: '', category: 'Benefits', isConfidential: false, isActive: false, defaultPriority: 'Low' },
    ]);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;

  it('lists every type, retired ones marked', () => {
    expect(el().querySelector('[data-testid="type-Old form"]')?.textContent).toContain('Retired');
  });

  it('builds a new type with fields and shows them in the live preview', async () => {
    (el().querySelector('[data-testid="new-type"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    const name = el().querySelector('[data-testid="type-name"]') as HTMLInputElement;
    name.value = 'Parking badge';
    name.dispatchEvent(new Event('input'));
    const label = el().querySelector('[data-testid="field-label"]') as HTMLInputElement;
    label.value = 'Licence plate';
    label.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(el().querySelector('[data-testid="preview"]')?.textContent).toContain('Licence plate');

    (el().querySelector('[data-testid="save-type"]') as HTMLButtonElement).click();
    const req = http.expectOne({ method: 'POST', url: '/api/request-types' });
    expect(req.request.body.name).toBe('Parking badge');
    expect(req.request.body.fields).toEqual([{ key: 'licencePlate', label: 'Licence plate', type: 'Text', required: false }]);
    req.flush({ id: 't3', name: 'Parking badge', description: '', category: 'Payroll', isConfidential: false, isActive: true, defaultPriority: 'Medium', fields: [] });
    TestBed.tick();
    http.expectOne((r) => r.url === '/api/request-types').flush([]);
  });

  it('shows the API explanation when the form definition is rejected', async () => {
    (el().querySelector('[data-testid="new-type"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    const name = el().querySelector('[data-testid="type-name"]') as HTMLInputElement;
    name.value = 'Broken';
    name.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    (el().querySelector('[data-testid="save-type"]') as HTMLButtonElement).click();
    http.expectOne({ method: 'POST', url: '/api/request-types' }).flush(
      { title: "Select field 'choice' needs at least one option.", code: 'form_schema.invalid' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await fixture.whenStable();

    expect(el().querySelector('[data-testid="type-error"]')?.textContent).toContain('needs at least one option');
  });
});
