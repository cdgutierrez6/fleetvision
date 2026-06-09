import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { AuthStore, AuthService, ViolationsStore } from '@fleetvision/shared/data-access';

@Component({
  selector: 'fv-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatButtonModule, MatMenuModule, MatTooltipModule, MatDividerModule],
  template: `
    <header class="header">
      <div class="header-left">
        <div class="connection-status" [class.connected]="violationsStore.isConnected()">
          <span class="status-dot"></span>
          <span class="status-text">
            {{ violationsStore.isConnected() ? 'En línea' : 'Reconectando...' }}
          </span>
        </div>
      </div>

      <div class="header-right">
        <div class="tenant-chip">
          <mat-icon class="tenant-icon">business</mat-icon>
          <span>{{ authStore.tenantName() }}</span>
        </div>

        <button mat-icon-button [matMenuTriggerFor]="userMenu" class="avatar-btn">
          <div class="avatar">{{ authStore.avatarInitial() }}</div>
        </button>

        <mat-menu #userMenu="matMenu" xPosition="before">
          <div class="user-menu-header">
            <div class="user-menu-name">{{ authStore.displayName() }}</div>
            <div class="user-menu-tenant">{{ authStore.tenantName() }}</div>
          </div>
          <mat-divider></mat-divider>
          <button mat-menu-item (click)="logout()">
            <mat-icon>logout</mat-icon>
            <span>Cerrar sesión</span>
          </button>
        </mat-menu>
      </div>
    </header>
  `,
  styles: [`
    .header {
      height: 56px;
      background: #fff;
      border-bottom: 1px solid #E0E4EA;
      display: flex; align-items: center; justify-content: space-between;
      padding: 0 24px;
      position: sticky; top: 0; z-index: 100;
    }
    .header-right { display: flex; align-items: center; gap: 16px; }
    .connection-status {
      display: flex; align-items: center; gap: 6px;
      font-size: 12px; color: #9E9E9E;
    }
    .status-dot {
      width: 8px; height: 8px; border-radius: 50%;
      background: #9E9E9E; transition: background .3s;
    }
    .connection-status.connected .status-dot { background: #4CAF50; }
    .connection-status.connected .status-text { color: #4CAF50; }
    .tenant-chip {
      display: flex; align-items: center; gap: 6px;
      background: #F5F7FA; border-radius: 20px;
      padding: 4px 12px; font-size: 12px; color: #1E3A5F; font-weight: 600;
    }
    .tenant-chip .tenant-icon { font-size: 14px; width: 14px; height: 14px; color: #1E3A5F; }
    .avatar-btn { padding: 0; }
    .avatar {
      width: 32px; height: 32px; border-radius: 50%;
      background: linear-gradient(135deg, #1E3A5F, #00BFA5);
      color: #fff; font-weight: 700; font-size: 13px;
      display: flex; align-items: center; justify-content: center;
    }
    .user-menu-header { padding: 12px 16px; }
    .user-menu-name { font-weight: 600; font-size: 14px; }
    .user-menu-tenant { font-size: 12px; color: #6B7280; margin-top: 2px; }
  `]
})
export class HeaderComponent {
  authStore = inject(AuthStore);
  violationsStore = inject(ViolationsStore);
  private authService = inject(AuthService);
  private router = inject(Router);

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
