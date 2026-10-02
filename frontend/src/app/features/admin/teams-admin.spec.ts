import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TeamInfo } from '../../core/api/api.models';
import { TeamsAdmin } from './teams-admin';

describe('TeamsAdmin', () => {
  const payroll: TeamInfo = {
    id: 'team-payroll',
    name: 'Payroll',
    strategy: 'LeastLoaded',
    members: [{ id: 'u-sami', fullName: 'Sami Gharbi', activeCases: 3 }],
    requestTypes: ['Payslip correction'],
  };

  it('lists teams with workload and saves edits', async () => {
    TestBed.configureTestingModule({ imports: [TeamsAdmin], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(TeamsAdmin);
    fixture.detectChanges();
    http.expectOne('/api/teams').flush([payroll]);
    http.expectOne((r) => r.url === '/api/users').flush({
      items: [
        { id: 'u-sami', fullName: 'Sami Gharbi', roles: ['Employee', 'PayrollSpecialist'], isActive: true },
        { id: 'u-amira', fullName: 'Amira Ben Salah', roles: ['Employee'], isActive: true },
      ],
      page: 1,
      pageSize: 100,
      totalCount: 2,
    });
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[data-testid="team-Payroll"]')?.textContent).toContain('Sami Gharbi 3 active');
    expect(el.textContent).toContain('Handles: Payslip correction');

    (el.querySelector('[aria-label="Edit Payroll"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    const req = http.expectOne('/api/teams/team-payroll');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ name: 'Payroll', strategy: 'LeastLoaded', memberIds: ['u-sami'] });
    req.flush(payroll);
  });
});
