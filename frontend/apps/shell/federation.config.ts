import { FederationConfig, withNativeFederation, shareAll } from '@angular-architects/native-federation';

const config: FederationConfig = {
  name: 'shell',
  remotes: {
    'mfe-fleet':      'http://localhost:4201/remoteEntry.json',
    'mfe-alerts':     'http://localhost:4202/remoteEntry.json',
    'mfe-monitoring': 'http://localhost:4203/remoteEntry.json',
    'mfe-admin':      'http://localhost:4204/remoteEntry.json',
    'mfe-reports':    'http://localhost:4205/remoteEntry.json',
    'mfe-billing':    'http://localhost:4206/remoteEntry.json',
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
    '@angular/cdk',
    '@angular/cdk/layout',
    '@angular/cdk/portal',
    '@angular/cdk/overlay',
    '@angular/cdk/scrolling',
    '@angular/cdk/collections',
    '@angular/cdk/a11y',
    '@angular/cdk/bidi',
    '@angular/cdk/coercion',
    '@angular/cdk/keycodes',
    '@angular/cdk/observers',
    '@angular/cdk/platform',
    '@angular/cdk/text-field',
    '@angular/cdk/tree',
    '@angular/cdk/table',
    '@angular/cdk/drag-drop',
    '@angular/cdk/stepper',
    '@angular/cdk/accordion',
  ],
};

export default withNativeFederation(config);
