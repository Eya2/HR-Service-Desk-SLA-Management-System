import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';

/** Route titles are translation keys; the tab shows "Page · HR Service Desk" in the current language. */
@Injectable({ providedIn: 'root' })
export class TranslatedTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly translate = inject(TranslateService);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const key = this.buildTitle(snapshot);
    const app = this.translate.instant('common.appName') as string;
    if (!key || key === 'common.appName') {
      this.title.setTitle(app);
      return;
    }
    this.title.setTitle(`${this.translate.instant(key) as string} · ${app}`);
  }
}
