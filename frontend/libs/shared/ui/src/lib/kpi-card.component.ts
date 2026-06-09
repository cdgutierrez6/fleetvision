import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'fv-kpi-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="kpi-card">
      <div class="kpi-icon" [style.background]="iconBg()">
        <mat-icon [style.color]="color()">{{ icon() }}</mat-icon>
      </div>
      <div class="kpi-content">
        <span class="kpi-value">{{ value() }}</span>
        <span class="kpi-label">{{ label() }}</span>
      </div>
    </div>
  `,
  styles: [`
    .kpi-card {
      display: flex;
      align-items: center;
      gap: 16px;
      background: #fff;
      border-radius: 12px;
      padding: 20px;
      box-shadow: 0 1px 4px rgba(0,0,0,.08);
      border: 1px solid #E0E4EA;
    }
    .kpi-icon {
      width: 48px;
      height: 48px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .kpi-content { display: flex; flex-direction: column; }
    .kpi-value {
      font-size: 28px;
      font-weight: 700;
      color: #1A2332;
      line-height: 1;
    }
    .kpi-label { font-size: 12px; color: #6B7280; margin-top: 4px; }
  `]
})
export class KpiCardComponent {
  label = input.required<string>();
  value = input.required<number>();
  icon = input.required<string>();
  color = input<string>('#1E3A5F');

  iconBg() {
    const hex = this.color();
    return `${hex}1A`;
  }
}
