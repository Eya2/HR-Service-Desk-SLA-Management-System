import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role } from './auth.models';
import { AuthService } from './auth.service';

/** Lets signed-in users through; others go to the login page and come back afterwards. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.isAuthenticated()) {
    return true;
  }
  return inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Keeps signed-in users away from the login page. */
export const guestGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() ? inject(Router).createUrlTree(['/']) : true;

/** Requires at least one of the given roles. The API enforces the same rules; this only shapes navigation. */
export function roleGuard(roles: readonly Role[]): CanActivateFn {
  return () => (inject(AuthService).hasAnyRole(roles) ? true : inject(Router).createUrlTree(['/forbidden']));
}
