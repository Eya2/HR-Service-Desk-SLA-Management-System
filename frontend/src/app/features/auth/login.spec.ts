import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { session } from '../../testing/auth-fixtures';
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
});
