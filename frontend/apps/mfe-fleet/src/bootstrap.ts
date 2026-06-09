import { bootstrapApplication } from '@angular/platform-browser';
import { Component } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

@Component({
  selector: 'fv-mfe-fleet-root',
  standalone: true,
  template: '<p>mfe-fleet standalone bootstrap</p>',
})
class MfeFleetRootComponent {}

bootstrapApplication(MfeFleetRootComponent, {
  providers: [provideAnimationsAsync()],
}).catch(err => console.error('[mfe-fleet] Bootstrap error:', err));
