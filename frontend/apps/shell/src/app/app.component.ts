import {
  ChangeDetectionStrategy, Component, OnInit, OnDestroy, effect, inject, computed, signal
} from '@angular/core';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Subscription, filter } from 'rxjs';
import {
  AuthStore, VehiclesStore, ViolationsStore, SignalRService
} from '@fleetvision/shared/data-access';
import { VIOLATION_TYPE_LABELS } from '@fleetvision/shared/models';
import { SidenavComponent } from './layout/sidenav.component';
import { HeaderComponent } from './layout/header.component';
import { LayoutService } from './core/layout.service';

const MOBILE_BP = '(max-width: 768px)';

@Component({
  selector: 'fv-root',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet, MatSidenavModule, MatSnackBarModule,
    SidenavComponent, HeaderComponent,
  ],
  template: `
    <mat-sidenav-container class="app-container">
      <mat-sidenav
        class="app-sidenav"
        [mode]="isMobile() ? 'over' : 'side'"
        [opened]="sidenavOpened()"
        (closed)="layout.close()"
        [disableClose]="!isMobile()"
      >
        <fv-sidenav />
      </mat-sidenav>

      <mat-sidenav-content class="app-content">
        @if (authStore.isAuthenticated()) {
          <fv-header />
        }
        <main class="main-content">
          <router-outlet />
        </main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`
    .app-container { height: 100vh; }
    .app-sidenav { width: 220px; border-right: none; }
    .app-content { display: flex; flex-direction: column; }
    .main-content {
      flex: 1; padding: 24px;
      background: #F5F7FA; min-height: calc(100vh - 56px);
      overflow-y: auto;
    }
    @media (max-width: 768px) {
      .main-content { padding: 16px; }
    }
    @media (max-width: 480px) {
      .main-content { padding: 12px; }
    }
  `]
})
export class AppComponent implements OnInit, OnDestroy {
  authStore = inject(AuthStore);
  layout = inject(LayoutService);
  private vehiclesStore = inject(VehiclesStore);
  private violationsStore = inject(ViolationsStore);
  private signalR = inject(SignalRService);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);

  isMobile = signal(window.matchMedia(MOBILE_BP).matches);
  sidenavOpened = computed(() =>
    this.isMobile() ? this.layout.sidenavOpen() : this.authStore.isAuthenticated()
  );

  private mobileHandler = (e: MediaQueryListEvent) => this.isMobile.set(e.matches);
  private mql = window.matchMedia(MOBILE_BP);
  private routerSub?: Subscription;

  constructor() {
    effect(() => {
      const violation = this.violationsStore.latestViolation();
      if (!violation) return;

      const vehicle = this.vehiclesStore.entityMap()[violation.vehicleId];
      const plate = vehicle?.plateNumber ?? 'Vehículo desconocido';
      const label = VIOLATION_TYPE_LABELS[violation.violationType];

      this.snackBar.open(`⚠ ${plate} — ${label}`, 'Ver alertas', {
        duration: 5000,
        horizontalPosition: 'end',
        verticalPosition: 'top',
        panelClass: ['violation-snack'],
      });
    });

    effect(() => {
      if (this.authStore.isAuthenticated()) {
        this.signalR.connect();
        this.vehiclesStore.loadAll();
      }
    });
  }

  ngOnInit(): void {
    this.mql.addEventListener('change', this.mobileHandler);
    this.routerSub = this.router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(() => {
        if (this.isMobile()) this.layout.close();
      });
  }

  ngOnDestroy(): void {
    this.signalR.disconnect();
    this.mql.removeEventListener('change', this.mobileHandler);
    this.routerSub?.unsubscribe();
  }
}
