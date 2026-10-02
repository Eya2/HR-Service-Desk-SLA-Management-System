import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay, tap, throwError } from 'rxjs';
import { Role, Session, UserProfile } from './auth.models';

/**
 * Holds the session in memory only (never in storage). After a page reload the session is
 * restored from the HttpOnly refresh cookie via {@link restoreSession}.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<Session | null>(null);
  private refreshInFlight: Observable<Session> | null = null;

  readonly user = computed<UserProfile | null>(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly accessToken = computed(() => this.session()?.accessToken ?? null);

  login(email: string, password: string, rememberMe = false): Observable<UserProfile> {
    return this.http.post<Session>('/api/auth/login', { email, password, rememberMe }).pipe(
      tap((session) => this.session.set(session)),
      map((session) => session.user),
    );
  }

  /** Rotates the refresh cookie and returns a new access token. Concurrent callers share one request. */
  refresh(): Observable<Session> {
    this.refreshInFlight ??= this.http.post<Session>('/api/auth/refresh', null).pipe(
      tap((session) => this.session.set(session)),
      catchError((error: unknown) => {
        this.session.set(null);
        return throwError(() => error);
      }),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.refreshInFlight;
  }

  /** Called at start-up: silently signs the user back in if a valid refresh cookie exists. */
  restoreSession(): Promise<void> {
    return firstValueFrom(
      this.refresh().pipe(
        map(() => undefined),
        catchError(() => of(undefined)),
      ),
    );
  }

  logout(): void {
    this.http
      .post<void>('/api/auth/logout', null)
      .pipe(catchError(() => of(undefined)))
      .subscribe(() => {
        this.session.set(null);
        void this.router.navigate(['/login']);
      });
  }

  /** Always succeeds from the user's point of view: the API never says whether the address exists. */
  requestPasswordReset(email: string): Observable<void> {
    return this.http.post<void>('/api/auth/forgot-password', { email });
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<void> {
    return this.http.post<void>('/api/auth/reset-password', { email, token, newPassword });
  }

  /** The refresh token was rejected: drop the session and send the user to sign in again. */
  handleSessionExpired(): void {
    this.session.set(null);
    const returnUrl = this.router.url;
    void this.router.navigate(['/login'], { queryParams: returnUrl && returnUrl !== '/' ? { returnUrl } : {} });
  }

  hasAnyRole(roles: readonly Role[]): boolean {
    const userRoles = this.user()?.roles ?? [];
    return roles.some((role) => userRoles.includes(role));
  }
}
