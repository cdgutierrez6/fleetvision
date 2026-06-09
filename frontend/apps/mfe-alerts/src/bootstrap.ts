import { bootstrapApplication } from '@angular/platform-browser';
import { Component } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

@Component({
  selector: 'fv-mfe-alerts-root',
  standalone: true,
  template: '<p>mfe-alerts standalone bootstrap</p>',
})
class MfeAlertsRootComponent {}

bootstrapApplication(MfeAlertsRootComponent, {
  providers: [provideAnimationsAsync()],
}).catch(err => console.error('[mfe-alerts] Bootstrap error:', err));
