import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '@fleetvision/shared/data-access';

@Component({
  selector: 'fv-auth-callback',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressSpinnerModule],
  template: `
    <div class="callback-page">
      <mat-spinner diameter="48" />
      <p>Verificando sesión...</p>
    </div>
  `,
  styles: [`
    .callback-page {
      min-height: 100vh;
      display: flex; flex-direction: column;
      align-items: center; justify-content: center;
      gap: 16px; color: #6B7280;
    }
  `]
})
export class AuthCallbackComponent implements OnInit {
  private authService = inject(AuthService);
  private router = inject(Router);

  async ngOnInit(): Promise<void> {
    if (this.authService.isLoggedIn()) {
      await this.router.navigate(['/fleet']);
    } else {
      await this.router.navigate(['/login']);
    }
  }
}
