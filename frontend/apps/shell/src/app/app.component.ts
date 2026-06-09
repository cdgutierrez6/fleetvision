import {
  ChangeDetectionStrategy, Component, OnInit, OnDestroy, effect, inject
} from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import {
  AuthStore, VehiclesStore, ViolationsStore, SignalRService
} from '@fleetvision/shared/data-access';
import { VIOLATION_TYPE_LABELS } from '@fleetvision/shared/models';
import { SidenavComponent } from './layout/sidenav.component';
import { HeaderComponent } from './layout/header.component';

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
        mode="side"
        [opened]="authStore.isAuthenticated()"
        [disableClose]="true"
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
  `]
})
export class AppComponent implements OnInit, OnDestroy {
  authStore = inject(AuthStore);
  private vehiclesStore = inject(VehiclesStore);
  private violationsStore = inject(ViolationsStore);
  private signalR = inject(SignalRService);
  private snackBar = inject(MatSnackBar);

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

  ngOnInit(): void { }

  ngOnDestroy(): void {
    this.signalR.disconnect();
  }
}
