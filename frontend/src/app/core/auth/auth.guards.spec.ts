import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Role } from './auth.models';
import { authGuard, guestGuard, roleGuard } from './auth.guards';
import { AuthService } from './auth.service';
import { session } from '../../testing/auth-fixtures';

describe('auth guards', () => {
  const route = {} as ActivatedRouteSnapshot;
  const state = { url: '/admin' } as RouterStateSnapshot;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
  });

  function signIn(roles: Role[]): void {
    TestBed.inject(AuthService).login('a@acme.example', 'x').subscribe();
    TestBed.inject(HttpTestingController).expectOne('/api/auth/login').flush(session('t', roles));
  }

  const run = (guard: typeof authGuard) => TestBed.runInInjectionContext(() => guard(route, state));
  const serialize = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  it('authGuard sends anonymous users to login with a return URL', () => {
    expect(serialize(run(authGuard))).toBe('/login?returnUrl=%2Fadmin');
  });

  it('authGuard lets signed-in users through', () => {
    signIn(['Employee']);
    expect(run(authGuard)).toBeTrue();
  });

  it('guestGuard sends signed-in users home', () => {
    signIn(['Employee']);
    expect(serialize(run(guestGuard))).toBe('/');
  });

  it('roleGuard allows any matching role and redirects others to /forbidden', () => {
    signIn(['Employee', 'Manager']);

    expect(run(roleGuard(['HrAdmin', 'Manager']))).toBeTrue();
    expect(serialize(run(roleGuard(['HrAdmin'])))).toBe('/forbidden');
  });
});
