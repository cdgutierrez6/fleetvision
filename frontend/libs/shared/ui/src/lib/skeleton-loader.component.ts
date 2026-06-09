import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'fv-skeleton-loader',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="skeleton" [style.height]="height()" [style.border-radius]="radius()"></div>`,
  styles: [`
    .skeleton {
      background: linear-gradient(90deg, #E0E4EA 25%, #F5F7FA 50%, #E0E4EA 75%);
      background-size: 200% 100%;
      animation: shimmer 1.4s infinite;
      width: 100%;
    }
    @keyframes shimmer {
      0% { background-position: 200% 0; }
      100% { background-position: -200% 0; }
    }
  `]
})
export class SkeletonLoaderComponent {
  height = input<string>('80px');
  radius = input<string>('8px');
}
