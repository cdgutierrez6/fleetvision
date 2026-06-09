import { initFederation } from '@angular-architects/native-federation';

initFederation()
  .catch(err => console.error('[mfe-fleet] Federation init error:', err))
  .then(() => import('./bootstrap'))
  .catch(err => console.error('[mfe-fleet] Bootstrap error:', err));
