import { FederationConfig, withNativeFederation, shareAll } from '@angular-architects/native-federation';

const config: FederationConfig = {
  name: 'mfe-billing',
  exposes: {
    './Billing': './apps/mfe-billing/src/app/billing/billing.component.ts',
  },
  shared: {
    ...shareAll({
      singleton: true,
      strictVersion: true,
      requiredVersion: 'auto',
    }),
  },
  skip: [
    'rxjs/ajax', 'rxjs/fetch', 'rxjs/testing', 'rxjs/webSocket',
    '@angular/compiler',
  ],
};

export default withNativeFederation(config);
