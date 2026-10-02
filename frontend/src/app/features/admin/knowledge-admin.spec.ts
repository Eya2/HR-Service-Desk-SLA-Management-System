import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { KnowledgeAdmin } from './knowledge-admin';

describe('KnowledgeAdmin', () => {
  let fixture: ComponentFixture<KnowledgeAdmin>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [KnowledgeAdmin],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(KnowledgeAdmin);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/knowledge' && r.params.get('includeUnpublished') === 'true').flush([
      { id: 'k1', title: 'Pay day', summary: '', category: 'Payroll', isPublished: false, viewCount: 7, helpfulCount: 2 },
    ]);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;

  it('lists drafts with their reads and helpful votes', () => {
    const row = el().querySelector('[data-testid="kb-Pay day"]');

    expect(row?.textContent).toContain('Draft');
    expect(row?.textContent).toContain('7');
    expect(row?.textContent).toContain('2');
  });

  it('creates a published article', async () => {
    (el().querySelector('[data-testid="new-article"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    const set = (selector: string, value: string) => {
      const input = el().querySelector(selector) as HTMLInputElement | HTMLTextAreaElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    set('[data-testid="kb-title"]', 'Training budget');
    set('[data-testid="kb-body"]', 'Ask your manager first.');
    (el().querySelector('[data-testid="kb-published"] button') as HTMLButtonElement).click();
    await fixture.whenStable();

    (el().querySelector('[data-testid="article-editor"]') as HTMLFormElement).dispatchEvent(new Event('submit'));
    const req = http.expectOne({ method: 'POST', url: '/api/knowledge' });
    expect(req.request.body).toEqual({ title: 'Training budget', summary: '', category: null, body: 'Ask your manager first.', isPublished: true });
    req.flush({ id: 'k2', title: 'Training budget', summary: '', body: 'Ask your manager first.', category: null, isPublished: true, viewCount: 0, helpfulCount: 0, updatedAt: null });
    TestBed.tick();
    http.expectOne((r) => r.url === '/api/knowledge').flush([]);
  });
});
