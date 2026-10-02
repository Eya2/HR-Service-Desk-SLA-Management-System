import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CalendarInfo, SlaPolicyInfo } from '../../core/api/api.models';
import { SlaAdmin } from './sla-admin';

describe('SlaAdmin', () => {
  let fixture: ComponentFixture<SlaAdmin>;
  let http: HttpTestingController;

  const calendar: CalendarInfo = {
    id: 'cal',
    name: 'Tunisia',
    timeZoneId: 'Africa/Tunis',
    workingHours: [{ day: 'Monday', start: '08:00', end: '12:00' }],
    holidays: [{ date: '2026-03-20', name: 'Independence Day' }],
  };
  const standard: SlaPolicyInfo = {
    id: 'p1',
    name: 'Standard',
    isDefault: true,
    atRiskThresholdPercent: 80,
    targets: [
      { priority: 'Low', firstResponseMinutes: 480, resolutionMinutes: 2400 },
      { priority: 'Medium', firstResponseMinutes: 240, resolutionMinutes: 1440 },
      { priority: 'High', firstResponseMinutes: 120, resolutionMinutes: 480 },
      { priority: 'Critical', firstResponseMinutes: 60, resolutionMinutes: 240 },
    ],
    pauseStatuses: ['PendingApproval', 'WaitingOnEmployee'],
    requestTypes: ['Work certificate'],
  };

  beforeEach(async () => {
    TestBed.configureTestingModule({ imports: [SlaAdmin], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SlaAdmin);
    fixture.detectChanges();
    http.expectOne('/api/calendar').flush(calendar);
    http.expectOne('/api/sla-policies').flush([standard]);
    await fixture.whenStable();
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('shows the calendar, its holidays and the policies', () => {
    expect(el().textContent).toContain('Africa/Tunis');
    expect(el().querySelector('[data-testid="holiday-2026-03-20"]')?.textContent).toContain('Independence Day');
    expect(el().querySelector('[data-testid="policy-Standard"]')?.textContent).toContain('default');
  });

  it('adds a holiday through the API', async () => {
    const inputs = el().querySelectorAll('form.inline')[0].querySelectorAll('input');
    (inputs[0] as HTMLInputElement).value = '2026-03-23';
    inputs[0].dispatchEvent(new Event('input'));
    (inputs[1] as HTMLInputElement).value = 'Company day';
    inputs[1].dispatchEvent(new Event('input'));
    await fixture.whenStable();

    (el().querySelector('[data-testid="add-holiday"]') as HTMLButtonElement).click();

    const req = http.expectOne('/api/calendar/holidays');
    expect(req.request.body).toEqual({ date: '2026-03-23', name: 'Company day' });
    req.flush(calendar);
  });

  it('saves policy targets in minutes', async () => {
    (el().querySelector('[aria-label="Edit Standard"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    (el().querySelector('[data-testid="save-policy"]') as HTMLButtonElement).click();

    const req = http.expectOne('/api/sla-policies/p1');
    expect(req.request.body.targets[0]).toEqual({ priority: 'Low', firstResponseMinutes: 480, resolutionMinutes: 2400 });
    expect(req.request.body.pauseStatuses).toEqual(['PendingApproval', 'WaitingOnEmployee']);
    req.flush(standard);
  });
});
