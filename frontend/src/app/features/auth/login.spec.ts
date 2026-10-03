import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { session } from '../../testing/auth-fixtures';
import { AuthService } from '../../core/auth/auth.service';
import { Login } from './login';

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
    fixture = TestBed.createComponent(Login);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;

  async function fillAndSubmit(email: string, password: string): Promise<void> {
    const [emailInput, passwordInput] = Array.from(el().querySelectorAll('input'));
    emailInput.value = email;
    emailInput.dispatchEvent(new Event('input'));
    passwordInput.value = password;
    passwordInput.dispatchEvent(new Event('input'));
    el().querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  it('does not call the API while the form is invalid', async () => {
    await fillAndSubmit('not-an-email', '');

    http.expectNone('/api/auth/login');
    expect(el().textContent).toContain('Enter a valid e-mail address.');
  });

  it('signs in and navigates to the return URL', async () => {
    fixture.componentRef.setInput('returnUrl', '/admin');
    await fillAndSubmit('nadia.jaziri@acme.example', 'Demo-Passw0rd!');

    const req = http.expectOne('/api/auth/login');
    expect(req.request.body.rememberMe).toBeFalse();
    req.flush(session());

    expect(router.navigateByUrl).toHaveBeenCalledWith('/admin');
  });

  it('ignores external return URLs', async () => {
    fixture.componentRef.setInput('returnUrl', '//evil.example');
    await fillAndSubmit('nadia.jaziri@acme.example', 'Demo-Passw0rd!');

    http.expectOne('/api/auth/login').flush(session());

    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('shows the API error message on failure', async () => {
    await fillAndSubmit('nadia.jaziri@acme.example', 'wrong');

    http
      .expectOne('/api/auth/login')
      .flush({ title: 'Invalid e-mail or password.', code: 'auth.invalid_credentials' }, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();

    expect(el().querySelector('[data-testid="login-error"]')?.textContent).toContain('Invalid e-mail or password.');
  });

  describe('single sign-on', () => {
    const typeEmail = async (email: string) => {
      const input = el().querySelector('input[formcontrolname="email"]') as HTMLInputElement;
      input.value = email;
      input.dispatchEvent(new Event('input'));
      await new Promise((resolve) => setTimeout(resolve, 400)); // past the typing pause
      TestBed.tick();
    };

    it("detects the organisation's provider from the e-mail and hides the password when SSO is required", async () => {
      await typeEmail('amira.bensalah@acme.example');
      const req = http.expectOne((r) => r.url === '/api/auth/sso/discover');
      expect(req.request.params.get('email')).toBe('amira.bensalah@acme.example');
      req.flush({ enabled: true, displayName: 'Microsoft', passwordLoginDisabled: true });
      await fixture.whenStable();

      expect(el().querySelector('[data-testid="sso-hint"]')?.textContent).toContain('Your organisation signs in with Microsoft.');
      expect(el().querySelector('input[formcontrolname="password"]')).toBeNull();
      expect(el().querySelector('[data-testid="sso-button"]')?.textContent).toContain('Continue with Microsoft');
    });

    it('asks for the e-mail first', async () => {
      (el().querySelector('[data-testid="sso-button"]') as HTMLButtonElement).click();
      await fixture.whenStable();

      expect(el().querySelector('[data-testid="login-error"]')?.textContent).toContain('Enter your work e-mail first');
      http.expectNone((r) => r.url === '/api/auth/sso/discover');
    });

    it('sends the browser to the identity provider through the API', async () => {
      const auth = TestBed.inject(AuthService);
      const navigate = spyOn(auth, 'navigateTo');
      fixture.componentRef.setInput('returnUrl', '/portal/requests');
      await typeEmail('amira.bensalah@acme.example');
      http.expectOne((r) => r.url === '/api/auth/sso/discover').flush({ enabled: true, displayName: 'Microsoft', passwordLoginDisabled: false });

      (el().querySelector('[data-testid="sso-button"]') as HTMLButtonElement).click();
      http.expectOne((r) => r.url === '/api/auth/sso/discover').flush({ enabled: true, displayName: 'Microsoft', passwordLoginDisabled: false });

      expect(navigate).toHaveBeenCalledWith('/api/auth/sso/start?email=amira.bensalah%40acme.example&returnUrl=%2Fportal%2Frequests&rememberMe=false');
    });

    it('explains a failed single sign-on', async () => {
      fixture.componentRef.setInput('ssoError', 'sso.no_account');
      fixture.componentInstance.ngOnInit();
      await fixture.whenStable();

      expect(el().querySelector('[data-testid="login-error"]')?.textContent).toContain('No active account matches your identity');
    });
  });
});

