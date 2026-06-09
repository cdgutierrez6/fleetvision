import { initFederation } from '@angular-architects/native-federation';

initFederation()
  .catch(err => console.error('[mfe-billing] Federation init error:', err))
  .then(() => import('./bootstrap'))
  .catch(err => console.error('[mfe-billing] Bootstrap error:', err));
