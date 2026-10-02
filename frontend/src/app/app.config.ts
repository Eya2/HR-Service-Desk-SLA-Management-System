import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { errorInterceptor } from './core/http/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // errorInterceptor wraps authInterceptor, so it only sees errors left after a refresh-and-retry.
    provideHttpClient(withFetch(), withInterceptors([errorInterceptor, authInterceptor])),
    // Restore the session from the refresh cookie before the first navigation.
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ],
};
