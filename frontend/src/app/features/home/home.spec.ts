import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Home } from './home';

describe('Home', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Home],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  async function render(respond: (http: HttpTestingController) => void): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges(); // starts the resource request; whenStable() would wait on it
    respond(http);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows API metadata when the API responds', async () => {
    const el = await render((h) =>
      h.expectOne('/api/system/info').flush({ name: 'HR Service Desk API', version: '1.0.0', environment: 'Docker' }),
    );

    expect(el.querySelector('[data-testid="api-status"]')?.textContent).toContain('HR Service Desk API');
    expect(el.querySelector('[data-testid="api-status"]')?.textContent).toContain('Docker');
  });

  it('shows an error when the API is unreachable', async () => {
    const el = await render((h) =>
      h.expectOne('/api/system/info').flush('down', { status: 502, statusText: 'Bad Gateway' }),
    );

    expect(el.querySelector('[data-testid="api-status"]')?.textContent).toContain('API unreachable');
  });
});
