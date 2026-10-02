import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./queue').then((m) => m.Queue),
    title: 'HR queue · HR Service Desk',
  },
];
