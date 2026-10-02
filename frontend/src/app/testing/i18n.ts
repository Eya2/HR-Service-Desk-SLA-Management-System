import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';

/** English text for a key, from the translations every spec loads (see test-providers.ts). */
export function text(key: string, params?: Record<string, unknown>): string {
  return TestBed.inject(TranslateService).instant(key, params) as string;
}
