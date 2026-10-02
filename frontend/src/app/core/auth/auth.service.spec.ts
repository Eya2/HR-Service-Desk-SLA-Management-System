import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { session } from '../../testing/auth-fixtures';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  });

  afterEach(() => http.verify());

  it('starts signed out', () => {
    expect(auth.isAuthenticated()).toBeFalse();
    expect(auth.user()).toBeNull();
    expect(auth.accessToken()).toBeNull();
  });

  it('stores the session in memory after login', () => {
    auth.login('amira.bensalah@acme.example', 'secret').subscribe();

    const req = http.expectOne('/api/auth/login');
    expect(req.request.body).toEqual({ email: 'amira.bensalah@acme.example', password: 'secret' });
    req.flush(session('abc', ['Employee', 'Manager']));

    expect(auth.isAuthenticated()).toBeTrue();
    expect(auth.accessToken()).toBe('abc');
    expect(auth.hasAnyRole(['Manager'])).toBeTrue();
    expect(auth.hasAnyRole(['HrAdmin', 'Auditor'])).toBeFalse();
  });

  it('shares one refresh request between concurrent callers', () => {
    const tokens: string[] = [];
    auth.refresh().subscribe((s) => tokens.push(s.accessToken));
    auth.refresh().subscribe((s) => tokens.push(s.accessToken));

    http.expectOne('/api/auth/refresh').flush(session('fresh'));

    expect(tokens).toEqual(['fresh', 'fresh']);
    expect(auth.accessToken()).toBe('fresh');
  });

  it('clears the session when refresh fails', () => {
    auth.login('a@acme.example', 'x').subscribe();
    http.expectOne('/api/auth/login').flush(session());

    auth.refresh().subscribe({ error: () => undefined });
    http.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.isAuthenticated()).toBeFalse();
  });

  it('restoreSession resolves whether or not a refresh cookie exists', async () => {
    const restored = auth.restoreSession();
    http.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expectAsync(restored).toBeResolved();
    expect(auth.isAuthenticated()).toBeFalse();
  });

  it('logout calls the API, clears the session and goes to the login page', () => {
    auth.login('a@acme.example', 'x').subscribe();
    http.expectOne('/api/auth/login').flush(session());

    auth.logout();
    http.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });

    expect(auth.isAuthenticated()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });
});
