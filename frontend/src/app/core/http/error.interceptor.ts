import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injector, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslateService } from '@ngx-translate/core';
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
  // Resolved on error only: the translation loader itself uses HttpClient, so injecting
  // TranslateService up front would be circular while that service is being created.
  const injector = inject(Injector);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status !== 400 && error.status !== 401) {
        const translate = injector.get(TranslateService);
        const message =
          error.status === 0 ? (translate.instant('errors.offline') as string) : problemMessage(translate, error);
        snackBar.open(message, translate.instant('common.dismiss') as string, { duration: 6000 });
      }
      return throwError(() => error);
    }),
  );
};

/**
 * The message for an API error: the translation of its code ("errors.<code>") when there is one,
 * else the API's own title, else the given fallback key.
 */
export function problemMessage(translate: TranslateService, error: unknown, fallbackKey = 'errors.generic'): string {
  const problem = problemOf(error);
  if (problem?.code) {
    const key = `errors.${problem.code}`;
    const translated = translate.instant(key) as string;
    if (translated !== key) return translated;
  }
  return problem?.title ?? (translate.instant(fallbackKey) as string);
}
