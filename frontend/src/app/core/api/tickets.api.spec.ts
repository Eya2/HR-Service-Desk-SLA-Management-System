import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TicketsApi } from './tickets.api';

describe('TicketsApi', () => {
  let api: TicketsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(TicketsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('submits a multipart body: JSON payload plus files named by field key', () => {
    const file = new File(['%PDF-1.7'], 'payslip.pdf');

    api
      .submit({ requestTypeId: 'type-1', title: 'March', description: '', values: { issue: 'other' }, files: { payslip: [file] } })
      .subscribe();

    const req = http.expectOne('/api/tickets');
    const body = req.request.body as FormData;
    expect(JSON.parse(body.get('payload') as string)).toEqual({
      requestTypeId: 'type-1',
      title: 'March',
      description: '',
      values: { issue: 'other' },
    });
    expect((body.get('payslip') as File).name).toBe('payslip.pdf');
    req.flush({ id: 't-1', reference: 'HR-2026-000001' });
  });

  it('passes list filters as query parameters', () => {
    api.mine({ status: 'New', search: null, page: 2, pageSize: 10 }).subscribe();

    const req = http.expectOne((r) => r.url === '/api/tickets/mine');
    expect(req.request.params.get('status')).toBe('New');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.has('search')).toBeFalse();
    req.flush({ items: [], page: 2, pageSize: 10, totalCount: 0 });
  });
});
