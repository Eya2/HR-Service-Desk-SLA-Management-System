import { Routes } from '@angular/router';
import { areaByPath } from './core/auth/areas';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
    title: 'Sign in · HR Service Desk',
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
        title: 'HR Service Desk',
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
        path: 'admin',
        canActivate: [roleGuard(areaByPath('admin').roles)],
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.routes),
      },
      {
        path: 'forbidden',
        loadComponent: () => import('./features/home/forbidden').then((m) => m.Forbidden),
        title: 'Access denied · HR Service Desk',
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
