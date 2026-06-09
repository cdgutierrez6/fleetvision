import { bootstrapApplication } from '@angular/platform-browser';
import { Component } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

@Component({
  selector: 'fv-mfe-billing-root',
  standalone: true,
  template: '<p>mfe-billing standalone bootstrap</p>',
})
class MfeBillingRootComponent {}

bootstrapApplication(MfeBillingRootComponent, {
  providers: [provideAnimationsAsync()],
}).catch(err => console.error('[mfe-billing] Bootstrap error:', err));
