import { bootstrapApplication } from '@angular/platform-browser';
import { Component } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

@Component({
  selector: 'fv-mfe-admin-root',
  standalone: true,
  template: '<p>mfe-admin standalone bootstrap</p>',
})
class MfeAdminRootComponent {}

bootstrapApplication(MfeAdminRootComponent, {
  providers: [provideAnimationsAsync()],
}).catch(err => console.error('[mfe-admin] Bootstrap error:', err));
