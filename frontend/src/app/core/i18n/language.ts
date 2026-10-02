import { registerLocaleData } from '@angular/common';
import localeAr from '@angular/common/locales/ar';
import localeFr from '@angular/common/locales/fr';

export type Language = 'en' | 'fr' | 'ar';

export interface LanguageOption {
  code: Language;
  /** The language's own name, so people find theirs whatever the current language. */
  nativeName: string;
  dir: 'ltr' | 'rtl';
}

export const LANGUAGES: readonly LanguageOption[] = [
  { code: 'en', nativeName: 'English', dir: 'ltr' },
  { code: 'fr', nativeName: 'Français', dir: 'ltr' },
  { code: 'ar', nativeName: 'العربية', dir: 'rtl' },
];

const STORAGE_KEY = 'hrdesk.language';

function isLanguage(value: string | null | undefined): value is Language {
  return LANGUAGES.some((l) => l.code === value);
}

function stored(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

/** The saved choice, else the browser's language when supported, else English. */
export function initialLanguage(): Language {
  const saved = stored();
  if (isLanguage(saved)) return saved;
  const browser = (globalThis.navigator?.language ?? 'en').slice(0, 2);
  return isLanguage(browser) ? browser : 'en';
}

export function saveLanguage(language: Language): void {
  try {
    localStorage.setItem(STORAGE_KEY, language);
  } catch {
    // Private mode: the choice lasts for this page only.
  }
}

/** Date and number formats; Arabic uses Western digits as is usual in Tunisian business documents. */
export function localeOf(language: Language): string {
  return { en: 'en-GB', fr: 'fr', ar: 'ar' }[language];
}

/** Sets lang and dir on <html> before Angular starts, so Material picks up the direction from the first render. */
export function applyDocumentLanguage(language: Language, doc: Document = document): void {
  registerLocaleData(localeFr);
  registerLocaleData(localeAr);
  const option = LANGUAGES.find((l) => l.code === language) ?? LANGUAGES[0];
  doc.documentElement.lang = option.code;
  doc.documentElement.dir = option.dir;
}
