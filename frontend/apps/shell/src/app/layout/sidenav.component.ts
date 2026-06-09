import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ViolationsStore } from '@fleetvision/shared/data-access';

interface NavItem {
  path: string;
  icon: string;
  label: string;
  badge?: boolean;
}

@Component({
  selector: 'fv-sidenav',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, MatIconModule, MatBadgeModule, MatTooltipModule],
  template: `
    <div class="sidenav">
      <div class="sidenav-logo">
        <mat-icon class="logo-icon">local_shipping</mat-icon>
        <span class="logo-text">FleetVision</span>
      </div>

      <nav class="sidenav-nav">
        @for (item of navItems; track item.path) {
          <a
            class="nav-item"
            [routerLink]="item.path"
            routerLinkActive="active"
            [matTooltip]="item.label"
            matTooltipPosition="right"
          >
            <mat-icon
              [matBadge]="item.badge && violationsStore.unreadCount() > 0 ? violationsStore.unreadCount().toString() : null"
              matBadgeColor="warn"
              matBadgeSize="small"
            >{{ item.icon }}</mat-icon>
            <span class="nav-label">{{ item.label }}</span>
          </a>
        }
      </nav>
    </div>
  `,
  styles: [`
    .sidenav {
      height: 100%;
      background: #1E3A5F;
      display: flex; flex-direction: column;
      padding: 0;
    }
    .sidenav-logo {
      display: flex; align-items: center; gap: 10px;
      padding: 20px 16px 16px;
      border-bottom: 1px solid rgba(255,255,255,.1);
      margin-bottom: 8px;
    }
    .logo-icon { color: #00BFA5; font-size: 28px; width: 28px; height: 28px; }
    .logo-text { color: #fff; font-size: 16px; font-weight: 700; letter-spacing: 0.5px; }
    .sidenav-nav { display: flex; flex-direction: column; gap: 2px; padding: 0 8px; }
    .nav-item {
      display: flex; align-items: center; gap: 12px;
      padding: 10px 12px; border-radius: 8px;
      color: rgba(255,255,255,.7);
      text-decoration: none; cursor: pointer;
      transition: background .15s, color .15s;
    }
    .nav-item:hover { background: rgba(255,255,255,.1); color: #fff; }
    .nav-item.active { background: rgba(0,191,165,.15); color: #00BFA5; }
    .nav-item mat-icon { font-size: 20px; width: 20px; height: 20px; }
    .nav-label { font-size: 13px; font-weight: 500; }
  `]
})
export class SidenavComponent {
  violationsStore = inject(ViolationsStore);

  navItems: NavItem[] = [
    { path: '/fleet', icon: 'directions_car', label: 'Flota' },
    { path: '/alerts', icon: 'notifications_active', label: 'Alertas', badge: true },
    { path: '/map', icon: 'map', label: 'Mapa en vivo' },
    { path: '/reports', icon: 'bar_chart', label: 'Reportes' },
    { path: '/admin', icon: 'admin_panel_settings', label: 'Administración' },
    { path: '/billing', icon: 'credit_card', label: 'Facturación' },
  ];
}
