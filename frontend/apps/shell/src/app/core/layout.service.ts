import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LayoutService {
  readonly sidenavOpen = signal(false);
  toggle(): void { this.sidenavOpen.update(v => !v); }
  close(): void { this.sidenavOpen.set(false); }
}
