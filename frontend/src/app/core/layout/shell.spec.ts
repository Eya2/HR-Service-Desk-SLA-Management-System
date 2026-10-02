import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Role } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { session } from '../../testing/auth-fixtures';
import { Shell } from './shell';

describe('Shell', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Shell],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  async function renderAs(roles: Role[]): Promise<HTMLElement> {
    TestBed.inject(AuthService).login('a@acme.example', 'x').subscribe();
    TestBed.inject(HttpTestingController).expectOne('/api/auth/login').flush(session('t', roles));
    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  const navItems = (el: HTMLElement) =>
    Array.from(el.querySelectorAll('[data-testid^="nav-"]')).map((a) => a.getAttribute('data-testid'));

  it('shows only the portal to an employee', async () => {
    expect(navItems(await renderAs(['Employee']))).toEqual(['nav-portal']);
  });

  it('shows every area an HR admin may open', async () => {
    expect(navItems(await renderAs(['Employee', 'HrAdmin']))).toEqual([
      'nav-portal',
      'nav-manager',
      'nav-agent',
      'nav-dashboard',
      'nav-admin',
    ]);
  });

  it('shows the user and organisation in the top bar', async () => {
    const el = await renderAs(['Auditor']);

    expect(el.textContent).toContain('Amira Ben Salah');
    expect(el.textContent).toContain('Acme Tunisie');
    expect(navItems(el)).toEqual(['nav-agent', 'nav-dashboard']);
  });
});
