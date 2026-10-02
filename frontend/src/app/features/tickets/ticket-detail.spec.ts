import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TicketDetails } from '../../core/api/api.models';
import { ticketDetails } from '../../testing/catalog-fixtures';
import { TicketDetail } from './ticket-detail';

describe('TicketDetail', () => {
  let fixture: ComponentFixture<TicketDetail>;
  let http: HttpTestingController;

  async function render(ticket: TicketDetails): Promise<HTMLElement> {
    TestBed.configureTestingModule({ imports: [TicketDetail], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TicketDetail);
    fixture.componentRef.setInput('id', ticket.id);
    fixture.detectChanges();
    http.expectOne(`/api/tickets/${ticket.id}`).flush(ticket);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the case, its answers and the conversation', async () => {
    const el = await render(
      ticketDetails({
        comments: [{ id: 'c1', authorId: 'u-2', authorName: 'Leila Mansour', body: 'We are on it.', isInternal: false, createdAt: '2026-03-02T10:00:00Z' }],
      }),
    );

    expect(el.querySelector('[data-testid="ticket-title"]')?.textContent).toContain('March payslip');
    expect(el.textContent).toContain('HR-2026-000042');
    expect(el.querySelector('[data-answer="payPeriod"]')?.textContent).toContain('2026-03');
    expect(el.querySelectorAll('[data-testid="comment"]').length).toBe(1);
  });

  it('offers internal notes only when the API allows them', async () => {
    let el = await render(ticketDetails());
    expect(el.querySelector('[data-testid="internal-toggle"]')).toBeNull();

    TestBed.resetTestingModule();
    el = await render(
      ticketDetails({ permissions: { canComment: true, canCommentInternally: true, canEdit: true, canChangePriority: true, canAttach: true, availableTransitions: [], decidableApprovalId: null } }),
    );
    expect(el.querySelector('[data-testid="internal-toggle"]')).not.toBeNull();
  });

  it('hides the reply box for read-only viewers', async () => {
    const el = await render(
      ticketDetails({ permissions: { canComment: false, canCommentInternally: false, canEdit: false, canChangePriority: false, canAttach: false, availableTransitions: [], decidableApprovalId: null } }),
    );

    expect(el.querySelector('[data-testid="comment-body"]')).toBeNull();
    expect(el.textContent).not.toContain('Add documents');
  });

  it('offers the transitions the API allows, applying simple ones directly', async () => {
    const el = await render(
      ticketDetails({
        status: 'Open',
        permissions: {
          canComment: true,
          canCommentInternally: true,
          canEdit: true,
          canChangePriority: true,
          canAttach: true,
          availableTransitions: ['InProgress', 'Rejected'],
          decidableApprovalId: null,
        },
      }),
    );

    const buttons = Array.from(el.querySelectorAll('[data-status]')).map((b) => b.getAttribute('data-status'));
    expect(buttons).toEqual(['InProgress', 'Rejected']);

    (el.querySelector('[data-status="InProgress"]') as HTMLButtonElement).click();
    const req = http.expectOne('/api/tickets/t-1/status');
    expect(req.request.body).toEqual({ status: 'InProgress', reason: null });
    req.flush(null, { status: 204, statusText: 'No Content' });
    TestBed.tick(); // runs the resource reload scheduled after success
    http.expectOne('/api/tickets/t-1').flush(ticketDetails({ status: 'InProgress' }));
  });

  it('shows the history of the case', async () => {
    const el = await render(
      ticketDetails({
        timeline: [
          { id: 'e1', type: 'Created', actorName: 'Amira Ben Salah', occurredAt: '2026-03-02T09:00:00Z', data: {} },
          {
            id: 'e2',
            type: 'StatusChanged',
            actorName: 'Leila Mansour',
            occurredAt: '2026-03-02T10:00:00Z',
            data: { from: 'Open', to: 'Rejected', reason: 'Already paid' },
          },
        ],
      }),
    );

    const entries = Array.from(el.querySelectorAll('[data-testid="timeline-entry"]')).map((e) => e.textContent ?? '');
    expect(entries[0]).toContain('Amira Ben Salah submitted the request');
    expect(entries[1]).toContain('Leila Mansour changed the status from Open to Rejected');
    expect(entries[1]).toContain('Already paid');
  });

  it('lets the current approver approve, and shows every step', async () => {
    const el = await render(
      ticketDetails({
        status: 'PendingApproval',
        approvals: [
          { id: 'a1', stepOrder: 1, stepName: 'Manager approval', approverRole: 'Manager', approverName: 'Youssef Haddad', decision: 'Pending', decidedByName: null, decidedAt: null, comment: null },
          { id: 'a2', stepOrder: 2, stepName: 'Payroll validation', approverRole: 'PayrollSpecialist', approverName: null, decision: 'Pending', decidedByName: null, decidedAt: null, comment: null },
        ],
        permissions: { canComment: true, canCommentInternally: false, canEdit: false, canChangePriority: false, canAttach: false, availableTransitions: [], decidableApprovalId: 'a1' },
      }),
    );

    expect(el.querySelector('[data-testid="approvals"]')?.textContent).toContain('Youssef Haddad');
    expect(el.querySelector('[data-testid="approvals"]')?.textContent).toContain('Payroll specialist');

    (el.querySelector('[data-testid="approve"]') as HTMLButtonElement).click();
    const req = http.expectOne('/api/tickets/t-1/approvals/a1/decision');
    expect(req.request.body).toEqual({ approve: true, comment: null });
    req.flush(null, { status: 204, statusText: 'No Content' });
    TestBed.tick();
    http.expectOne('/api/tickets/t-1').flush(ticketDetails({ status: 'PendingApproval' }));
  });

  it('hides approval buttons from people who cannot decide', async () => {
    const el = await render(
      ticketDetails({
        status: 'PendingApproval',
        approvals: [
          { id: 'a1', stepOrder: 1, stepName: 'Manager approval', approverRole: 'Manager', approverName: 'Youssef Haddad', decision: 'Pending', decidedByName: null, decidedAt: null, comment: null },
        ],
      }),
    );

    expect(el.querySelector('[data-testid="approve"]')).toBeNull();
  });

  it('appends a sent reply to the conversation', async () => {
    const el = await render(ticketDetails());
    const textarea = el.querySelector('[data-testid="comment-body"]') as HTMLTextAreaElement;
    textarea.value = 'Any update?';
    textarea.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    (el.querySelector('form.reply') as HTMLFormElement).dispatchEvent(new Event('submit'));
    const req = http.expectOne('/api/tickets/t-1/comments');
    expect(req.request.body).toEqual({ body: 'Any update?', isInternal: false });
    req.flush({ id: 'c9', authorId: 'u-1', authorName: 'Amira Ben Salah', body: 'Any update?', isInternal: false, createdAt: '2026-03-02T11:00:00Z' });
    await fixture.whenStable();

    expect(el.querySelectorAll('[data-testid="comment"]').length).toBe(1);
    expect(textarea.value).toBe('');
  });
});
