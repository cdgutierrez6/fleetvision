import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { ViolationEvent, VIOLATION_TYPE_LABELS, VIOLATION_TYPE_ICONS } from '@fleetvision/shared/models';

@Component({
  selector: 'fv-violation-alert',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="alert-card">
      <div class="alert-icon">
        <mat-icon>{{ icon() }}</mat-icon>
      </div>
      <div class="alert-body">
        <div class="alert-title">{{ label() }}</div>
        <div class="alert-meta">
          <span class="alert-geofence">{{ event().geofenceName }}</span>
          @if (event().actualSpeedKmh) {
            <span class="alert-speed">{{ event().actualSpeedKmh }} km/h</span>
          }
        </div>
        <div class="alert-time">{{ formatTime(event().occurredAt) }}</div>
      </div>
    </div>
  `,
  styles: [`
    .alert-card {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 12px 16px;
      background: #fff;
      border-left: 3px solid #F44336;
      border-radius: 0 8px 8px 0;
      box-shadow: 0 1px 3px rgba(0,0,0,.08);
    }
    .alert-icon {
      width: 36px;
      height: 36px;
      border-radius: 8px;
      background: #FFEBEE;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .alert-icon mat-icon { color: #F44336; font-size: 18px; width: 18px; height: 18px; }
    .alert-body { flex: 1; min-width: 0; }
    .alert-title { font-weight: 600; font-size: 13px; color: #1A2332; }
    .alert-meta { display: flex; gap: 8px; margin-top: 2px; }
    .alert-geofence { font-size: 12px; color: #6B7280; }
    .alert-speed {
      font-size: 11px;
      background: #FFEBEE;
      color: #F44336;
      padding: 1px 6px;
      border-radius: 8px;
      font-weight: 600;
    }
    .alert-time { font-size: 11px; color: #9E9E9E; margin-top: 4px; }
  `]
})
export class ViolationAlertComponent {
  event = input.required<ViolationEvent>();

  label() { return VIOLATION_TYPE_LABELS[this.event().violationType]; }
  icon() { return VIOLATION_TYPE_ICONS[this.event().violationType]; }

  formatTime(iso: string): string {
    return new Date(iso).toLocaleString('es', {
      day: '2-digit', month: '2-digit',
      hour: '2-digit', minute: '2-digit',
    });
  }
}
