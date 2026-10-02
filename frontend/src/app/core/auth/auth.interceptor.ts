import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const SESSION_ENDPOINTS = ['/api/auth/login', '/api/auth/refresh', '/api/auth/logout'];

function withToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
}

/**
 * Adds the access token to API calls. On a 401 it refreshes the session once (shared between
 * concurrent requests) and replays the request; if the refresh fails the user must sign in again.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/') || SESSION_ENDPOINTS.includes(request.url)) {
    return next(request);
  }

  const auth = inject(AuthService);
  return next(withToken(request, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }
      return auth.refresh().pipe(
        catchError(() => {
          auth.handleSessionExpired();
          return throwError(() => error);
        }),
        switchMap((session) => next(withToken(request, session.accessToken))),
      );
    }),
  );
};
