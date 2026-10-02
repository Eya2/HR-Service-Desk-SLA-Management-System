import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuditLogPage } from './audit-log';

describe('AuditLogPage', () => {
  it('lists audit entries with a link to the case', async () => {
    TestBed.configureTestingModule({ imports: [AuditLogPage], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(AuditLogPage);
    fixture.detectChanges();

    const req = http.expectOne((r) => r.url === '/api/audit-logs');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.has('action')).toBeFalse();
    req.flush({
      items: [
        {
          id: 'a1',
          occurredAt: '2026-03-23T09:00:00Z',
          userId: 'u-sami',
          userName: 'Sami Gharbi',
          action: 'SensitiveCaseViewed',
          entityType: 'Ticket',
          entityId: 't-1',
          summary: 'HR-2026-000042 viewed',
        },
      ],
      page: 1,
      pageSize: 50,
      totalCount: 1,
    });
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelectorAll('[data-testid="audit-row"]').length).toBe(1);
    expect(el.textContent).toContain('Sami Gharbi');
    expect(el.textContent).toContain('Sensitive case viewed');
    expect(el.querySelector('a')?.getAttribute('href')).toBe('/tickets/t-1');
  });
});
