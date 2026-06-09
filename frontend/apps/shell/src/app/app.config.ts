import { ApplicationConfig } from '@angular/core';
import {
  provideRouter, withEnabledBlockingInitialNavigation
} from '@angular/router';
import {
  provideHttpClient, withInterceptors, withFetch
} from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { appRoutes } from './app.routes';
import { tokenInterceptor } from './core/token.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(appRoutes, withEnabledBlockingInitialNavigation()),
    provideHttpClient(withInterceptors([tokenInterceptor]), withFetch()),
    provideAnimationsAsync(),
  ],
};
