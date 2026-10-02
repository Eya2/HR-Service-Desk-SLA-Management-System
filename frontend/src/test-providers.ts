import { EnvironmentProviders, Provider } from '@angular/core';
import { TranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import en from '../public/i18n/en.json';

/** English texts, loaded synchronously, so specs assert on the real wording. */
class EnglishLoader implements TranslateLoader {
  getTranslation() {
    return of(en);
  }
}

const providers: (Provider | EnvironmentProviders)[] = [provideTranslateService({ lang: 'en', fallbackLang: 'en', loader: EnglishLoader })];

export default providers;
