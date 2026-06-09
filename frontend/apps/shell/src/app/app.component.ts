import {
  ChangeDetectionStrategy, Component, OnInit, OnDestroy, effect, inject, computed
} from '@angular/core';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BreakpointObserver } from '@angular/cdk/layout';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { filter, map } from 'rxjs';
import {
  AuthStore, VehiclesStore, ViolationsStore, SignalRService
} from '@fleetvision/shared/data-access';
import { VIOLATION_TYPE_LABELS } from '@fleetvision/shared/models';
import { SidenavComponent } from './layout/sidenav.component';
import { HeaderComponent } from './layout/header.component';
import { LayoutService } from './core/layout.service';

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
  private breakpointObserver = inject(BreakpointObserver);
  private vehiclesStore = inject(VehiclesStore);
  private violationsStore = inject(ViolationsStore);
  private signalR = inject(SignalRService);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);

  isMobile = toSignal(
    this.breakpointObserver.observe('(max-width: 768px)').pipe(map(s => s.matches)),
    { initialValue: false }
  );

  sidenavOpened = computed(() =>
    this.isMobile() ? this.layout.sidenavOpen() : this.authStore.isAuthenticated()
  );

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

    // Close the overlay sidenav on mobile when navigating to a new route
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      takeUntilDestroyed()
    ).subscribe(() => {
      if (this.isMobile()) this.layout.close();
    });
  }

  ngOnInit(): void { }

  ngOnDestroy(): void {
    this.signalR.disconnect();
  }
}
