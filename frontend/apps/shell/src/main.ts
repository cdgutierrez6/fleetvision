import { initFederation } from '@angular-architects/native-federation';
import { environment } from './environments/environment';

const isProd = environment.production;
const base = isProd ? '' : 'http://localhost';

initFederation({
  'mfe-fleet':      `${base}:4201/remoteEntry.json`,
  'mfe-alerts':     `${base}:4202/remoteEntry.json`,
  'mfe-monitoring': `${base}:4203/remoteEntry.json`,
  'mfe-admin':      `${base}:4204/remoteEntry.json`,
  'mfe-reports':    `${base}:4205/remoteEntry.json`,
  'mfe-billing':    `${base}:4206/remoteEntry.json`,
})
  .catch(err => console.error('[Federation] Init error:', err))
  .then(() => import('./bootstrap'))
  .catch(err => console.error('[Bootstrap] Load error:', err));
