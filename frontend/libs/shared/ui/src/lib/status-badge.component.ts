import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Vehicle } from '@fleetvision/shared/models';

@Component({
  selector: 'fv-status-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="badge" [style.background]="bgColor()" [style.color]="textColor()">
      {{ label() }}
    </span>
  `,
  styles: [`
    .badge {
      display: inline-flex;
      align-items: center;
      padding: 2px 10px;
      border-radius: 12px;
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.4px;
      white-space: nowrap;
    }
  `]
})
export class StatusBadgeComponent {
  status = input.required<Vehicle['status']>();

  label() {
    const labels: Record<Vehicle['status'], string> = {
      Active: 'Activo',
      Inactive: 'Inactivo',
      Maintenance: 'Mantenimiento',
    };
    return labels[this.status()];
  }

  bgColor() {
    const colors: Record<Vehicle['status'], string> = {
      Active: '#E8F5E9',
      Inactive: '#F5F5F5',
      Maintenance: '#FFF3E0',
    };
    return colors[this.status()];
  }

  textColor() {
    const colors: Record<Vehicle['status'], string> = {
      Active: '#2E7D32',
      Inactive: '#616161',
      Maintenance: '#E65100',
    };
    return colors[this.status()];
  }
}
