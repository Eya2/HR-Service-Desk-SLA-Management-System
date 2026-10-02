import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Role } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { TicketSummary } from '../../core/api/api.models';
import { session } from '../../testing/auth-fixtures';
import { Queue } from './queue';

describe('Queue', () => {
  let fixture: ComponentFixture<Queue>;
  let http: HttpTestingController;

  const row = (reference: string, assigneeName: string | null): TicketSummary => ({
    id: `id-${reference}`,
    reference,
    title: 'Certificate',
    requestTypeId: 'rt',
    requestTypeName: 'Work certificate',
    category: 'Certificates',
    status: 'New',
    priority: 'Low',
    isConfidential: false,
    requesterId: 'u-1',
    requesterName: 'Amira Ben Salah',
    teamId: 'team-1',
    teamName: 'HR Service Center',
    assigneeId: assigneeName ? 'u-2' : null,
    assigneeName,
    slaState: 'AtRisk',
    resolutionDueAt: '2026-03-02T16:00:00Z',
    createdAt: '2026-03-02T09:00:00Z',
    updatedAt: null,
  });

  async function render(roles: Role[], rows: TicketSummary[]): Promise<{ el: HTMLElement; request: TestRequest }> {
    TestBed.configureTestingModule({ imports: [Queue], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).login('a@acme.example', 'x').subscribe();
    http.expectOne('/api/auth/login').flush(session('t', roles));
    fixture = TestBed.createComponent(Queue);
    fixture.detectChanges();
    const request = http.expectOne((r) => r.url === '/api/tickets');
    request.flush({ items: rows, page: 1, pageSize: 20, totalCount: rows.length });
    await fixture.whenStable();
    return { el: fixture.nativeElement as HTMLElement, request };
  }

  it('opens on "My cases" for HR staff, active cases only', async () => {
    const { el, request } = await render(['HrOfficer'], []);

    expect(request.request.params.get('scope')).toBe('Mine');
    expect(request.request.params.get('activeOnly')).toBe('true');
    expect(el.querySelector('[data-testid="scope-Unassigned"]')).not.toBeNull();
  });

  it('gives auditors a read-only view of all cases', async () => {
    const { el, request } = await render(['Auditor'], [row('HR-2026-000001', null)]);

    expect(request.request.params.get('scope')).toBe('All');
    expect(el.querySelector('[data-testid="scope-Mine"]')).toBeNull();
    expect(el.querySelector('[data-testid="take-HR-2026-000001"]')).toBeNull();
  });

  it('lets an agent take an unassigned case', async () => {
    const { el } = await render(['HrOfficer'], [row('HR-2026-000001', null), row('HR-2026-000002', 'Nadia Jaziri')]);

    expect(el.querySelector('[data-testid="take-HR-2026-000002"]')).toBeNull();
    (el.querySelector('[data-testid="take-HR-2026-000001"]') as HTMLButtonElement).click();

    http.expectOne('/api/tickets/id-HR-2026-000001/claim').flush(null, { status: 204, statusText: 'No Content' });
    TestBed.tick();
    http.expectOne((r) => r.url === '/api/tickets').flush({ items: [], page: 1, pageSize: 20, totalCount: 0 });
  });
});
