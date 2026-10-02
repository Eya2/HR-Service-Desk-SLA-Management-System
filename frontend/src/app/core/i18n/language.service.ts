import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { LANGUAGES, Language, initialLanguage, saveLanguage } from './language';

/**
 * The interface language. Texts switch through ngx-translate; dates, numbers and the text direction
 * are fixed at start-up (LOCALE_ID, dir), so changing language saves the choice and reloads the page.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);
  private readonly document = inject(DOCUMENT);

  readonly languages = LANGUAGES;
  readonly current = signal<Language>(initialLanguage());
  readonly option = computed(() => LANGUAGES.find((l) => l.code === this.current()) ?? LANGUAGES[0]);

  /** Loads the current language's texts (app initializer). */
  init(): Promise<unknown> {
    return new Promise((resolve) => this.translate.use(this.current()).subscribe({ next: resolve, error: resolve }));
  }

  change(language: Language): void {
    if (language === this.current()) return;
    saveLanguage(language);
    this.document.defaultView?.location.reload();
  }
}
