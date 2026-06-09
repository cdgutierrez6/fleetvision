import { bootstrapApplication } from '@angular/platform-browser';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

@Component({
  selector: 'fv-mfe-reports-root',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '<p style="padding:16px;color:#666">mfe-reports standalone bootstrap</p>',
})
class MfeReportsRootComponent {}

bootstrapApplication(MfeReportsRootComponent, {
  providers: [
    provideHttpClient(withFetch()),
    provideAnimationsAsync(),
  ],
}).catch(err => console.error(err));
