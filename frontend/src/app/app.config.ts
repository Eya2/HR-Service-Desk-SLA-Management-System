import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, LOCALE_ID, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { MAT_BUTTON_TOGGLE_DEFAULT_OPTIONS } from '@angular/material/button-toggle';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { TitleStrategy, provideRouter, withComponentInputBinding } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { errorInterceptor } from './core/http/error.interceptor';
import { initialLanguage, localeOf } from './core/i18n/language';
import { LanguageService } from './core/i18n/language.service';
import { TranslatedPaginatorIntl } from './core/i18n/paginator-intl';
import { TranslatedTitleStrategy } from './core/i18n/translated-title';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // errorInterceptor wraps authInterceptor, so it only sees errors left after a refresh-and-retry.
    provideHttpClient(withFetch(), withInterceptors([errorInterceptor, authInterceptor])),
    provideTranslateService({ fallbackLang: 'en', loader: provideTranslateHttpLoader({ prefix: '/i18n/', suffix: '.json' }) }),
    { provide: TitleStrategy, useExisting: TranslatedTitleStrategy },
    { provide: MatPaginatorIntl, useClass: TranslatedPaginatorIntl },
    // Segmented buttons without a check mark, and outlined fields everywhere by default.
    { provide: MAT_BUTTON_TOGGLE_DEFAULT_OPTIONS, useValue: { hideSingleSelectionIndicator: true, hideMultipleSelectionIndicator: true } },
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'outline' } },
    { provide: LOCALE_ID, useFactory: () => localeOf(initialLanguage()) },
    // Texts first, then the session from the refresh cookie, before the first navigation.
    provideAppInitializer(() => inject(LanguageService).init()),
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ],
};
