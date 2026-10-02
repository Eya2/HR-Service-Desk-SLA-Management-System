import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { errorInterceptor } from './error.interceptor';

describe('errorInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj<MatSnackBar>('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
      ],
    });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
  });

  function fail(status: number, body: object | null = null): void {
    client.get('/api/x').subscribe({ error: () => undefined });
    http.expectOne('/api/x').flush(body, { status, statusText: 'Error' });
  }

  it('shows the problem title for server and permission errors', () => {
    fail(409, { title: 'The resource was modified by someone else.' });

    expect(snackBar.open).toHaveBeenCalledWith('The resource was modified by someone else.', 'Dismiss', jasmine.any(Object));
  });

  it('falls back to a generic message when there is no problem body', () => {
    fail(500);

    expect(snackBar.open).toHaveBeenCalledWith('Something went wrong. Please try again.', 'Dismiss', jasmine.any(Object));
  });

  it('leaves validation and authentication errors to the caller', () => {
    fail(400, { title: 'Validation' });
    fail(401, { title: 'Unauthorized' });

    expect(snackBar.open).not.toHaveBeenCalled();
  });
});
