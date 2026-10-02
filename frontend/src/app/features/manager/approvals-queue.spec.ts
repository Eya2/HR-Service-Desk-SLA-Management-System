import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { PendingApproval } from '../../core/api/api.models';
import { ApprovalsQueue } from './approvals-queue';

describe('ApprovalsQueue', () => {
  let fixture: ComponentFixture<ApprovalsQueue>;
  let http: HttpTestingController;
  let dialog: jasmine.SpyObj<MatDialog>;

  const item = (reference: string, approvalId: string): PendingApproval => ({
    approvalId,
    ticketId: `t-${approvalId}`,
    reference,
    title: 'March payslip',
    requestTypeName: 'Payslip correction',
    requesterName: 'Amira Ben Salah',
    stepName: 'Manager approval',
    stepOrder: 1,
    stepCount: 2,
    submittedAt: '2026-03-02T09:00:00Z',
  });

  beforeEach(async () => {
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    TestBed.configureTestingModule({
      imports: [ApprovalsQueue],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: MatDialog, useValue: dialog }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ApprovalsQueue);
    fixture.detectChanges();
    http.expectOne('/api/approvals/pending').flush([item('HR-2026-000001', 'a1'), item('HR-2026-000002', 'a2')]);
    await fixture.whenStable();
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('lists each waiting request with its step', () => {
    expect(el().querySelectorAll('mat-card').length).toBe(2);
    expect(el().textContent).toContain('Step 1 of 2: Manager approval');
  });

  it('approves in one click and removes the request from the queue', async () => {
    (el().querySelector('[data-testid="approval-HR-2026-000001"] [data-testid="approve"]') as HTMLButtonElement).click();

    const req = http.expectOne('/api/tickets/t-a1/approvals/a1/decision');
    expect(req.request.body).toEqual({ approve: true, comment: null });
    req.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(el().querySelectorAll('mat-card').length).toBe(1);
  });

  it('asks for a reason before rejecting', async () => {
    dialog.open.and.returnValue({ afterClosed: () => of('Already granted') } as never);
    const reject = Array.from(el().querySelectorAll('[data-testid="approval-HR-2026-000002"] button')).find((b) =>
      b.textContent?.includes('Reject'),
    ) as HTMLButtonElement;

    reject.click();

    expect(dialog.open).toHaveBeenCalled();
    const req = http.expectOne('/api/tickets/t-a2/approvals/a2/decision');
    expect(req.request.body).toEqual({ approve: false, comment: 'Already granted' });
    req.flush(null, { status: 204, statusText: 'No Content' });
  });
});
