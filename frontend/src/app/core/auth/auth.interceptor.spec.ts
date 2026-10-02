import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { session } from '../../testing/auth-fixtures';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([])],
    });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);

    auth.login('a@acme.example', 'x').subscribe();
    http.expectOne('/api/auth/login').flush(session('old-token'));
  });

  afterEach(() => http.verify());

  it('adds the bearer token to API calls', () => {
    client.get('/api/users').subscribe();

    expect(http.expectOne('/api/users').request.headers.get('Authorization')).toBe('Bearer old-token');
  });

  it('does not add the token to non-API or session endpoints', () => {
    client.get('/assets/i18n/fr.json').subscribe();
    client.post('/api/auth/logout', null).subscribe();

    expect(http.expectOne('/assets/i18n/fr.json').request.headers.has('Authorization')).toBeFalse();
    expect(http.expectOne('/api/auth/logout').request.headers.has('Authorization')).toBeFalse();
  });

  it('refreshes once on 401 and replays the request with the new token', () => {
    let body: unknown;
    client.get('/api/auth/me').subscribe((b) => (body = b));

    http.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/auth/refresh').flush(session('new-token'));
    const retry = http.expectOne('/api/auth/me');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
    retry.flush({ ok: true });

    expect(body).toEqual({ ok: true });
  });

  it('ends the session when the refresh is rejected', () => {
    spyOn(auth, 'handleSessionExpired');
    let failure: HttpErrorResponse | undefined;
    client.get('/api/auth/me').subscribe({ error: (e: HttpErrorResponse) => (failure = e) });

    http.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.handleSessionExpired).toHaveBeenCalled();
    expect(failure?.status).toBe(401);
  });

  it('passes other errors through untouched', () => {
    let failure: HttpErrorResponse | undefined;
    client.get('/api/users').subscribe({ error: (e: HttpErrorResponse) => (failure = e) });

    http.expectOne('/api/users').flush(null, { status: 403, statusText: 'Forbidden' });

    expect(failure?.status).toBe(403);
  });
});
