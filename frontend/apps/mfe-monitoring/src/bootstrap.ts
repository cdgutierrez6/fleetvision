import { bootstrapApplication } from '@angular/platform-browser';
import { Component } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

@Component({
  selector: 'fv-mfe-monitoring-root',
  standalone: true,
  template: '<p>mfe-monitoring standalone bootstrap</p>',
})
class MfeMonitoringRootComponent {}

bootstrapApplication(MfeMonitoringRootComponent, {
  providers: [provideAnimationsAsync()],
}).catch(err => console.error('[mfe-monitoring] Bootstrap error:', err));
