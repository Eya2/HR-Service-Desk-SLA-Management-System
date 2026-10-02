import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ForgotPassword } from './forgot-password';
import { PASSWORD_RULES, ResetPassword } from './reset-password';

describe('password pages', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
  });

  function type(el: HTMLElement, selector: string, value: string): void {
    const input = el.querySelector(selector) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  it('shows the same confirmation whether or not the address exists', async () => {
    const fixture = TestBed.createComponent(ForgotPassword);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    type(el, 'input[type="email"]', 'nobody@acme.example');
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    http.expectOne('/api/auth/forgot-password').flush(null, { status: 429, statusText: 'Too Many Requests' });
    await fixture.whenStable();

    expect(el.querySelector('[data-testid="reset-sent"]')?.textContent).toContain('nobody@acme.example');
  });

  it('checks the policy while typing and sends the token with the new password', async () => {
    const fixture: ComponentFixture<ResetPassword> = TestBed.createComponent(ResetPassword);
    fixture.componentRef.setInput('email', 'amira@acme.example');
    fixture.componentRef.setInput('token', 'abc');
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const submit = () => el.querySelector('button[type="submit"]') as HTMLButtonElement;

    type(el, 'input[formcontrolname="password"]', 'short');
    await fixture.whenStable();
    expect(el.querySelectorAll('.rules li.ok').length).toBe(1);
    expect(submit().disabled).toBeTrue();

    type(el, 'input[formcontrolname="password"]', 'Brand-New-Passw0rd!');
    type(el, 'input[formcontrolname="confirm"]', 'Brand-New-Passw0rd!');
    await fixture.whenStable();
    expect(el.querySelectorAll('.rules li.ok').length).toBe(PASSWORD_RULES.length);
    submit().click();

    const req = http.expectOne('/api/auth/reset-password');
    expect(req.request.body).toEqual({ email: 'amira@acme.example', token: 'abc', newPassword: 'Brand-New-Passw0rd!' });
    req.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    expect(el.querySelector('[data-testid="reset-done"]')).not.toBeNull();
  });

  it('explains an incomplete link', async () => {
    const fixture = TestBed.createComponent(ResetPassword);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('This link is incomplete');
  });
});
