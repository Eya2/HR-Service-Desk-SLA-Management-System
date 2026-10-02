import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { WebhookInfo } from '../../core/api/integrations.api';
import { IntegrationsAdmin } from './integrations-admin';

const webhook: WebhookInfo = {
  id: 'w1',
  name: 'Payroll system',
  url: 'https://payroll.example/hooks',
  events: ['ticket.status_changed'],
  isActive: true,
  createdAt: '2026-05-01T09:00:00Z',
  pendingDeliveries: 0,
  failedDeliveries: 1,
  lastDeliveredAt: null,
};

describe('IntegrationsAdmin', () => {
  let fixture: ComponentFixture<IntegrationsAdmin>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ imports: [IntegrationsAdmin], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(IntegrationsAdmin);
    fixture.detectChanges();
    http.expectOne('/api/integrations/api-keys').flush([
      { id: 'k1', name: 'HRIS', prefix: 'abcd2345', scopes: ['tickets:read'], createdAt: '2026-05-01T09:00:00Z', lastUsedAt: null, revokedAt: null },
    ]);
    http.expectOne('/api/integrations/webhooks').flush([webhook]);
    http.expectOne('/api/integrations/catalog').flush({ scopes: ['tickets:read', 'tickets:write'], events: ['ticket.created', 'ticket.status_changed'] });
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;

  it('lists keys without their secret and webhooks with failed deliveries', () => {
    expect(el().querySelector('[data-testid="key-HRIS"]')?.textContent).toContain('hrd_abcd2345_…');
    expect(el().querySelector('[data-testid="webhook-Payroll system"]')?.textContent).toContain('1 failed');
  });

  it('shows a new key once, with a warning to copy it', async () => {
    const name = el().querySelector('[data-testid="key-name"]') as HTMLInputElement;
    name.value = 'Payroll';
    name.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    (el().querySelector('[data-testid="create-key"]') as HTMLButtonElement).click();

    const req = http.expectOne({ method: 'POST', url: '/api/integrations/api-keys' });
    expect(req.request.body).toEqual({ name: 'Payroll', scopes: ['tickets:read'] });
    req.flush({ key: { id: 'k2', name: 'Payroll', prefix: 'zzzz2345', scopes: ['tickets:read'], createdAt: '', lastUsedAt: null, revokedAt: null }, secret: 'hrd_zzzz2345_secret' });
    TestBed.tick();
    http.expectOne('/api/integrations/api-keys').flush([]);
    await fixture.whenStable();

    const secret = el().querySelector('[data-testid="secret"]');
    expect(secret?.textContent).toContain('hrd_zzzz2345_secret');
    expect(secret?.textContent).toContain('will not be shown again');
  });

  it('opens the delivery history and sends a failed delivery again', async () => {
    (el().querySelector('[data-testid="deliveries-Payroll system"]') as HTMLButtonElement).click();
    TestBed.tick();
    http.expectOne((r) => r.url === '/api/integrations/webhooks/w1/deliveries').flush({
      items: [
        { id: 'd1', eventType: 'ticket.status_changed', ticketId: 't1', status: 'Failed', attempts: 6, createdAt: '2026-05-01T09:00:00Z', lastAttemptAt: null, nextAttemptAt: null, lastStatusCode: 503, lastError: 'HTTP 503', payload: '{}' },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    await fixture.whenStable();

    const table = el().querySelector('[data-testid="delivery-table"]');
    expect(table?.textContent).toContain('503');
    expect(table?.textContent).toContain('Failed');

    const again = Array.from(table!.querySelectorAll('button')).find((b) => b.textContent?.includes('Send again')) as HTMLButtonElement;
    again.click();
    http.expectOne({ method: 'POST', url: '/api/integrations/deliveries/d1/redeliver' }).flush(null, { status: 204, statusText: 'No Content' });
    TestBed.tick();
    http.match(() => true).forEach((r) => r.flush(r.request.url.includes('deliveries') ? { items: [], page: 1, pageSize: 20, totalCount: 0 } : []));
  });
});
