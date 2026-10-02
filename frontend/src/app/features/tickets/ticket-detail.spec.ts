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
      ticketDetails({ permissions: { canComment: true, canCommentInternally: true, canEdit: true, canChangePriority: true, canAttach: true } }),
    );
    expect(el.querySelector('[data-testid="internal-toggle"]')).not.toBeNull();
  });

  it('hides the reply box for read-only viewers', async () => {
    const el = await render(
      ticketDetails({ permissions: { canComment: false, canCommentInternally: false, canEdit: false, canChangePriority: false, canAttach: false } }),
    );

    expect(el.querySelector('[data-testid="comment-body"]')).toBeNull();
    expect(el.textContent).not.toContain('Add documents');
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
