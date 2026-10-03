import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SsoAdmin } from './sso-admin';

describe('SsoAdmin', () => {
  let fixture: ComponentFixture<SsoAdmin>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ imports: [SsoAdmin], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SsoAdmin);
    fixture.detectChanges();
    http.expectOne('/api/settings/sso').flush({
      isEnabled: true,
      displayName: 'Microsoft',
      authority: 'https://login.microsoftonline.com/tid/v2.0',
      metadataAddress: null,
      clientId: 'client-id',
      hasClientSecret: true,
      emailDomains: ['acme.example', 'acme.tn'],
      autoProvision: true,
      passwordLoginDisabled: false,
      callbackUrl: 'https://hr.acme.example/api/auth/sso/callback',
    });
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;

  it('shows the redirect URI to register and keeps the saved secret when the field is left empty', async () => {
    expect(el().querySelector('[data-testid="sso-callback"]')?.textContent).toContain('https://hr.acme.example/api/auth/sso/callback');

    (el().querySelector('[data-testid="sso-form"]') as HTMLFormElement).dispatchEvent(new Event('submit'));
    const req = http.expectOne({ method: 'PUT', url: '/api/settings/sso' });
    expect(req.request.body.clientSecret).toBeNull();
    expect(req.request.body.emailDomains).toEqual(['acme.example', 'acme.tn']);
    req.flush({ ...req.request.body, hasClientSecret: true, callbackUrl: 'x' });
  });

  it('tests the connection with the provider', async () => {
    (el().querySelector('[data-testid="sso-test"]') as HTMLButtonElement).click();
    http.expectOne({ method: 'POST', url: '/api/settings/sso/test' }).flush({
      issuer: 'https://login.microsoftonline.com/tid/v2.0',
      authorizationEndpoint: 'a',
      tokenEndpoint: 't',
    });
    await fixture.whenStable();

    expect(el().querySelector('[data-testid="sso-test-ok"]')?.textContent).toContain('https://login.microsoftonline.com/tid/v2.0');
  });
});
