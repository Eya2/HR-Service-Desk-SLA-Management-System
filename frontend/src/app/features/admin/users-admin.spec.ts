import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UsersAdmin } from './users-admin';

const page = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });

describe('UsersAdmin', () => {
  let fixture: ComponentFixture<UsersAdmin>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ imports: [UsersAdmin], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(UsersAdmin);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/users' && !r.params.has('role')).flush(
      page([
        { id: 'u1', email: 'amira.bensalah@acme.example', fullName: 'Amira Ben Salah', roles: ['Employee'], isActive: true, lastLoginAt: null },
        { id: 'u2', email: 'old@acme.example', fullName: 'Old Account', roles: ['Employee'], isActive: false, lastLoginAt: null },
      ]),
    );
    http.expectOne((r) => r.url === '/api/users' && r.params.get('role') === 'Manager').flush(
      page([{ id: 'm1', email: 'youssef.haddad@acme.example', fullName: 'Youssef Haddad', roles: ['Employee', 'Manager'], isActive: true, lastLoginAt: null }]),
    );
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;

  it('lists accounts with their roles and marks inactive ones', () => {
    expect(el().querySelector('[data-testid="user-amira.bensalah@acme.example"]')?.textContent).toContain('Employee');
    expect(el().querySelector('[data-testid="user-old@acme.example"]')?.textContent).toContain('Inactive');
  });

  it('creates a user with roles, a manager and a policy-compliant password', async () => {
    (el().querySelector('[data-testid="new-user"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    const set = (id: string, value: string) => {
      const input = el().querySelector(`[data-testid="${id}"]`) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    set('first-name', 'Mouna');
    set('last-name', 'Saidi');
    set('email', 'mouna.saidi@acme.example');
    set('password', 'weak');
    await fixture.whenStable();
    expect((el().querySelector('[data-testid="save-user"]') as HTMLButtonElement).disabled).toBeTrue();

    set('password', 'Strong-Passw0rd!');
    (el().querySelector('[data-testid="role-HrOfficer"] input') as HTMLInputElement).click();
    await fixture.whenStable();
    (el().querySelector('[data-testid="user-editor"]') as HTMLFormElement).dispatchEvent(new Event('submit'));

    const req = http.expectOne({ method: 'POST', url: '/api/users' });
    expect(req.request.body).toEqual({
      email: 'mouna.saidi@acme.example',
      firstName: 'Mouna',
      lastName: 'Saidi',
      roles: ['Employee', 'HrOfficer'],
      managerId: null,
      password: 'Strong-Passw0rd!',
    });
    req.flush({});
    TestBed.tick();
    http.match((r) => r.url === '/api/users').forEach((r) => r.flush(page([])));
  });
});
