import { Routes } from '@angular/router';
import { PortalLayout } from './portal-layout';

export const routes: Routes = [
  {
    path: '',
    component: PortalLayout,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'catalog' },
      {
        path: 'catalog',
        loadComponent: () => import('./catalog').then((m) => m.Catalog),
        title: 'Request catalog · HR Service Desk',
      },
      {
        path: 'requests',
        loadComponent: () => import('./my-requests').then((m) => m.MyRequests),
        title: 'My requests · HR Service Desk',
      },
    ],
  },
  {
    path: 'new/:typeId',
    loadComponent: () => import('./new-request').then((m) => m.NewRequest),
    title: 'New request · HR Service Desk',
  },
];
