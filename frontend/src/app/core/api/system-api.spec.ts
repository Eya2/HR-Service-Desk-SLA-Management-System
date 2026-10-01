import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SystemApi, SystemInfo } from './system-api';

describe('SystemApi', () => {
  let api: SystemApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(SystemApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('GETs /api/system/info', () => {
    const expected: SystemInfo = { name: 'HR Service Desk API', version: '1.0.0', environment: 'Development' };
    let actual: SystemInfo | undefined;

    api.getInfo().subscribe((info) => (actual = info));
    const req = http.expectOne('/api/system/info');
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });
});
