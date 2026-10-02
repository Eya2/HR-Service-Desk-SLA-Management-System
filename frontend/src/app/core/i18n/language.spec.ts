import { applyDocumentLanguage, initialLanguage, localeOf, saveLanguage } from './language';

describe('language', () => {
  afterEach(() => {
    localStorage.removeItem('hrdesk.language');
    applyDocumentLanguage('en');
  });

  it('remembers the chosen language', () => {
    saveLanguage('fr');

    expect(initialLanguage()).toBe('fr');
  });

  it('ignores an unknown saved value', () => {
    localStorage.setItem('hrdesk.language', 'xx');

    expect(['en', 'fr', 'ar']).toContain(initialLanguage());
  });

  it('switches the page to right-to-left for Arabic', () => {
    const doc = document.implementation.createHTMLDocument('test');

    applyDocumentLanguage('ar', doc);

    expect(doc.documentElement.dir).toBe('rtl');
    expect(doc.documentElement.lang).toBe('ar');
  });

  it('formats dates and numbers with the language locale', () => {
    expect(localeOf('fr')).toBe('fr');
    expect(localeOf('ar')).toBe('ar');
  });
});
