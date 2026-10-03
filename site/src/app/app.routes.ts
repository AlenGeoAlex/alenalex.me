import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'Alen Alex · backend engineer',
    loadComponent: () => import('./features/home/home.component').then((m) => m.HomeComponent),
  },
  {
    path: 'writing',
    title: 'writing · Alen Alex',
    loadComponent: () => import('./features/writing/writing-index/writing-index.component').then((m) => m.WritingIndexComponent),
  },
  {
    // a standalone post, or a series page
    path: 'writing/:slug',
    loadComponent: () => import('./features/writing/writing-entry/writing-entry.component').then((m) => m.WritingEntryComponent),
  },
  {
    // a part of a series
    path: 'writing/:slug/:part',
    loadComponent: () => import('./features/writing/writing-entry/writing-entry.component').then((m) => m.WritingEntryComponent),
  },
  {
    path: 'work',
    title: 'work · Alen Alex',
    loadComponent: () => import('./features/work/work.component').then((m) => m.WorkComponent),
  },
  {
    path: 'guestbook',
    title: 'guestbook · Alen Alex',
    loadComponent: () => import('./features/guestbook/guestbook-page/guestbook-page.component').then((m) => m.GuestbookPageComponent),
  },
  {
    path: 'now',
    title: 'now · Alen Alex',
    loadComponent: () => import('./features/now/now.component').then((m) => m.NowComponent),
  },
  {
    path: '**',
    title: 'not in the catalog · Alen Alex',
    loadComponent: () => import('./features/not-found/not-found.component').then((m) => m.NotFoundComponent),
  },
];
