import { Component, OnInit, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/auth/auth.service';
import { AuthLayout } from './auth-layout';

/**
 * Landing page after the identity provider: the API has set the session cookie, so the session is
 * restored and the user continues where they were going.
 */
@Component({
  selector: 'app-sso-complete',
  imports: [AuthLayout, TranslatePipe],
  template: `
    <app-auth-layout>
      <div class="finishing" role="status">
        <span class="auth-spinner big" aria-hidden="true"></span>
        <p>{{ 'auth.ssoFinishing' | translate }}</p>
      </div>
    </app-auth-layout>
  `,
  styles: `
    .finishing {
      display: grid;
      justify-items: center;
      gap: 16px;
      color: var(--app-muted);
    }
    .big {
      width: 36px;
      height: 36px;
      border-width: 3px;
      border-color: color-mix(in srgb, var(--mat-sys-primary) 25%, transparent);
      border-top-color: var(--mat-sys-primary);
    }
  `,
})
export class SsoComplete implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Query parameter set by the API's callback. */
  readonly returnUrl = input<string>();

  async ngOnInit(): Promise<void> {
    if (!this.auth.isAuthenticated()) await this.auth.restoreSession();
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/login'], { queryParams: { ssoError: 'sso.failed' } });
      return;
    }

    const url = this.returnUrl();
    void this.router.navigateByUrl(url && url.startsWith('/') && !url.startsWith('//') ? url : '/');
  }
}
