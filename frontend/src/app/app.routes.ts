import { Routes } from '@angular/router';
import { areaByPath } from './core/auth/areas';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
    title: 'title.signIn',
  },
  {
    path: 'forgot-password',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/forgot-password').then((m) => m.ForgotPassword),
    title: 'title.forgotPassword',
  },
  {
    path: 'reset-password',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/reset-password').then((m) => m.ResetPassword),
    title: 'title.newPassword',
  },
  {
    // After the identity provider: no guard, the page restores the session itself.
    path: 'sso/complete',
    loadComponent: () => import('./features/auth/sso-complete').then((m) => m.SsoComplete),
    title: 'title.sso',
  },
  {
    path: '',
    loadComponent: () => import('./core/layout/shell').then((m) => m.Shell),
    canActivate: [authGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./features/home/home').then((m) => m.Home),
        title: 'common.appName',
      },
      {
        path: 'portal',
        canActivate: [roleGuard(areaByPath('portal').roles)],
        loadChildren: () => import('./features/portal/portal.routes').then((m) => m.routes),
      },
      {
        path: 'manager',
        canActivate: [roleGuard(areaByPath('manager').roles)],
        loadChildren: () => import('./features/manager/manager.routes').then((m) => m.routes),
      },
      {
        path: 'agent',
        canActivate: [roleGuard(areaByPath('agent').roles)],
        loadChildren: () => import('./features/agent/agent.routes').then((m) => m.routes),
      },
      {
        path: 'dashboard',
        canActivate: [roleGuard(areaByPath('dashboard').roles)],
        loadChildren: () => import('./features/dashboard/dashboard.routes').then((m) => m.routes),
      },
      {
        path: 'audit',
        canActivate: [roleGuard(areaByPath('audit').roles)],
        loadComponent: () => import('./features/audit/audit-log').then((m) => m.AuditLogPage),
        title: 'title.audit',
      },
      {
        path: 'admin',
        canActivate: [roleGuard(areaByPath('admin').roles)],
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.routes),
      },
      {
        // Any signed-in user; the API returns 404 for cases the caller may not see.
        path: 'tickets/:id',
        loadComponent: () => import('./features/tickets/ticket-detail').then((m) => m.TicketDetail),
        title: 'title.case',
      },
      {
        path: 'forbidden',
        loadComponent: () => import('./features/home/forbidden').then((m) => m.Forbidden),
        title: 'title.forbidden',
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
