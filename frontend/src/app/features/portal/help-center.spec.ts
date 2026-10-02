import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ArticleSummary } from '../../core/api/api.models';
import { ArticlePage } from './article-page';
import { HelpCenter } from './help-center';

const article: ArticleSummary = {
  id: 'k1',
  title: 'When is salary paid?',
  summary: 'Pay day and payslips.',
  category: 'Payroll',
  isPublished: true,
  viewCount: 10,
  helpfulCount: 4,
};

describe('HelpCenter', () => {
  let fixture: ComponentFixture<HelpCenter>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HelpCenter], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HelpCenter);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('lists the published articles with their category', async () => {
    http.expectOne('/api/knowledge').flush([article]);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[data-testid="article-k1"]')?.textContent).toContain('When is salary paid?');
    expect(el.textContent).toContain('Payroll');
    expect(el.textContent).toContain('1 article(s)');
  });

  it('searches as the employee types', async () => {
    http.expectOne('/api/knowledge').flush([article]);
    await fixture.whenStable();
    const input = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="help-search"]') as HTMLInputElement;
    input.value = 'congé';
    input.dispatchEvent(new Event('input'));
    await new Promise((resolve) => setTimeout(resolve, 300));
    TestBed.tick();

    http.expectOne((r) => r.url === '/api/knowledge' && r.params.get('q') === 'congé').flush([]);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No article matches your search.');
  });
});

describe('ArticlePage', () => {
  let fixture: ComponentFixture<ArticlePage>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ imports: [ArticlePage], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ArticlePage);
    fixture.componentRef.setInput('articleId', 'k1');
    fixture.detectChanges();
    http.expectOne('/api/knowledge/k1').flush({ ...article, body: 'First paragraph.\n\nSecond paragraph.', updatedAt: null });
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('shows the article as paragraphs', () => {
    const body = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="article-body"]');

    expect(body?.querySelectorAll('p').length).toBe(2);
  });

  it('records "this answered my question" once and thanks the employee', async () => {
    const el = fixture.nativeElement as HTMLElement;
    (el.querySelector('[data-testid="helpful"]') as HTMLButtonElement).click();
    http.expectOne('/api/knowledge/k1/helpful').flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(el.querySelector('[data-testid="helpful"]')).toBeNull();
    expect(el.querySelector('[data-testid="helpful-thanks"]')?.textContent).toContain('Glad it helped');
  });
});
