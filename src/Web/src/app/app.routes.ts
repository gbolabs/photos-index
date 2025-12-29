import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/dashboard/dashboard').then(m => m.Dashboard)
  },
  {
    path: 'indexing',
    loadComponent: () => import('./features/indexing/indexing').then(m => m.Indexing)
  },
  {
    path: 'gallery',
    loadComponent: () => import('./features/gallery/gallery').then(m => m.GalleryComponent)
  },
  {
    path: 'files',
    loadComponent: () => import('./features/files/files').then(m => m.Files)
  },
  {
    path: 'files/:id',
    loadComponent: () => import('./features/files/file-detail/file-detail.component').then(m => m.FileDetailComponent)
  },
  {
    path: 'duplicates',
    loadComponent: () => import('./features/duplicates/duplicates').then(m => m.Duplicates)
  },
  {
    path: 'settings',
    loadComponent: () => import('./features/settings/settings').then(m => m.Settings)
  },
  {
    path: 'admin',
    loadComponent: () => import('./features/admin/admin').then(m => m.Admin)
  },
  { path: '**', redirectTo: '' }
];
