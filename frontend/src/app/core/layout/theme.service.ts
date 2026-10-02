import { Injectable, effect, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'hrdesk.theme';

/** Light, dark or the OS setting; remembered on this device. Charts read `effectiveDark` to pick their palette. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly mode = signal<ThemeMode>(ThemeService.read());
  /** Bumped whenever the rendered scheme may have changed (charts re-read their colors). */
  readonly version = signal(0);

  private readonly media = typeof matchMedia === 'function' ? matchMedia('(prefers-color-scheme: dark)') : null;

  constructor() {
    this.media?.addEventListener('change', () => this.version.update((v) => v + 1));
    effect(() => {
      const mode = this.mode();
      const root = document.documentElement;
      if (mode === 'system') {
        root.removeAttribute('data-theme');
        root.style.colorScheme = 'light dark';
      } else {
        root.setAttribute('data-theme', mode);
        root.style.colorScheme = mode;
      }
      try {
        localStorage.setItem(STORAGE_KEY, mode);
      } catch {
        // Storage can be unavailable (private mode); the choice then lasts for this visit.
      }
      this.version.update((v) => v + 1);
    });
  }

  get effectiveDark(): boolean {
    const mode = this.mode();
    return mode === 'dark' || (mode === 'system' && !!this.media?.matches);
  }

  cycle(): void {
    this.mode.update((m) => (m === 'light' ? 'dark' : m === 'dark' ? 'system' : 'light'));
  }

  private static read(): ThemeMode {
    try {
      const value = localStorage.getItem(STORAGE_KEY);
      return value === 'light' || value === 'dark' || value === 'system' ? value : 'system';
    } catch {
      return 'system';
    }
  }
}
