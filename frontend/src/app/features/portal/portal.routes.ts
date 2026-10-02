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
        title: 'title.catalog',
      },
      {
        path: 'help',
        loadComponent: () => import('./help-center').then((m) => m.HelpCenter),
        title: 'title.help',
      },
      {
        path: 'help/:articleId',
        loadComponent: () => import('./article-page').then((m) => m.ArticlePage),
        title: 'title.help',
      },
      {
        path: 'requests',
        loadComponent: () => import('./my-requests').then((m) => m.MyRequests),
        title: 'title.myRequests',
      },
    ],
  },
  {
    path: 'new/:typeId',
    loadComponent: () => import('./new-request').then((m) => m.NewRequest),
    title: 'title.newRequest',
  },
];
