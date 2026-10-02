import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';

/** RFC 7807 body returned by the API for every error. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

export function problemOf(error: unknown): ProblemDetails | null {
  return error instanceof HttpErrorResponse && error.error && typeof error.error === 'object'
    ? (error.error as ProblemDetails)
    : null;
}

/**
 * Shows a snack bar for errors no screen handles itself. Validation (400) and authentication (401)
 * errors are left to the calling form and the auth interceptor.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const snackBar = inject(MatSnackBar);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status !== 400 && error.status !== 401) {
        const message =
          error.status === 0
            ? 'The server cannot be reached. Check your connection.'
            : (problemOf(error)?.title ?? 'Something went wrong. Please try again.');
        snackBar.open(message, 'Dismiss', { duration: 6000 });
      }
      return throwError(() => error);
    }),
  );
};
