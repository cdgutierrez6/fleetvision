import { Routes } from '@angular/router';
import { loadRemoteModule } from '@angular-architects/native-federation';
import { authGuard } from './core/auth.guard';
import { LoginComponent } from './pages/login/login.component';
import { AuthCallbackComponent } from './pages/auth-callback/auth-callback.component';

export const appRoutes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'auth/callback', component: AuthCallbackComponent },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'fleet', pathMatch: 'full' },
      {
        path: 'fleet',
        loadComponent: () =>
          loadRemoteModule('mfe-fleet', './Dashboard').then(m => m.DashboardComponent),
      },
      {
        path: 'alerts',
        loadComponent: () =>
          loadRemoteModule('mfe-alerts', './Feed').then(m => m.FeedComponent),
      },
      {
        path: 'map',
        loadComponent: () =>
          loadRemoteModule('mfe-monitoring', './Map').then(m => m.MapComponent),
      },
      {
        path: 'admin',
        loadComponent: () =>
          loadRemoteModule('mfe-admin', './Admin').then(m => m.AdminComponent),
      },
      {
        path: 'reports',
        loadComponent: () =>
          loadRemoteModule('mfe-reports', './Reports').then(m => m.ReportsComponent),
      },
      {
        path: 'billing',
        loadComponent: () =>
          loadRemoteModule('mfe-billing', './Billing').then(m => m.BillingComponent),
      },
    ],
  },
  { path: '**', redirectTo: 'fleet' },
];
