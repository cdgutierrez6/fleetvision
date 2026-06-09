import {
  ChangeDetectionStrategy, Component, inject, OnInit, signal
} from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AuthService, AuthStore } from '@fleetvision/shared/data-access';

@Component({
  selector: 'fv-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule, MatButtonModule, MatInputModule,
    MatFormFieldModule, MatProgressSpinnerModule, MatIconModule,
  ],
  template: `
    <div class="login-page">
      <div class="login-card">
        <div class="login-brand">
          <div class="brand-icon">
            <mat-icon>local_shipping</mat-icon>
          </div>
          <h1 class="brand-name">FleetVision</h1>
          <p class="brand-sub">Telemetría de flotas en tiempo real</p>
        </div>

        <div class="login-divider"></div>

        @if (error()) {
          <div class="login-error">
            <mat-icon>error_outline</mat-icon>
            <span>{{ error() }}</span>
          </div>
        }

        <mat-form-field appearance="outline" class="login-field">
          <mat-label>Correo electrónico</mat-label>
          <input matInput type="email" [(ngModel)]="email" [disabled]="loading()" />
          <mat-icon matPrefix>email</mat-icon>
        </mat-form-field>

        <mat-form-field appearance="outline" class="login-field">
          <mat-label>Contraseña</mat-label>
          <input matInput [type]="showPassword() ? 'text' : 'password'"
            [(ngModel)]="password" [disabled]="loading()"
            (keyup.enter)="login()" />
          <mat-icon matPrefix>lock</mat-icon>
          <button mat-icon-button matSuffix (click)="showPassword.set(!showPassword())" type="button">
            <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
          </button>
        </mat-form-field>

        <button
          mat-flat-button
          class="login-btn"
          [disabled]="loading() || !email || !password"
          (click)="login()"
        >
          @if (loading()) {
            <mat-spinner diameter="20" />
          } @else {
            <mat-icon>login</mat-icon>
            Ingresar
          }
        </button>

        <p class="login-footer">Plataforma segura — JWT + TLS</p>
      </div>
    </div>
  `,
  styles: [`
    .login-page {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #1E3A5F 0%, #0D2240 60%, #00BFA5 100%);
    }
    .login-card {
      background: #fff;
      border-radius: 16px;
      padding: 48px 40px;
      width: 100%;
      max-width: 420px;
      box-shadow: 0 20px 60px rgba(0,0,0,.25);
      text-align: center;
    }
    .login-brand { margin-bottom: 8px; }
    .brand-icon {
      width: 64px; height: 64px;
      border-radius: 16px;
      background: linear-gradient(135deg, #1E3A5F, #00BFA5);
      display: inline-flex; align-items: center; justify-content: center;
      margin-bottom: 12px;
    }
    .brand-icon mat-icon { color: #fff; font-size: 32px; width: 32px; height: 32px; }
    .brand-name { font-size: 28px; font-weight: 800; color: #1E3A5F; margin: 0; }
    .brand-sub { font-size: 13px; color: #6B7280; margin: 4px 0 0; }
    .login-divider { height: 1px; background: #E0E4EA; margin: 24px 0; }
    .login-error {
      display: flex; align-items: center; gap: 8px;
      background: #FFEBEE; color: #C62828;
      border-radius: 8px; padding: 10px 14px;
      font-size: 13px; margin-bottom: 16px; text-align: left;
    }
    .login-error mat-icon { font-size: 18px; width: 18px; height: 18px; flex-shrink: 0; }
    .login-field { width: 100%; margin-bottom: 8px; }
    .login-btn {
      width: 100%; height: 48px; border-radius: 10px;
      background: #1E3A5F; color: #fff; font-size: 15px; font-weight: 600;
      display: flex; align-items: center; justify-content: center; gap: 8px;
      margin-top: 8px;
    }
    .login-btn:hover:not(:disabled) { background: #2d5080; }
    .login-footer { font-size: 11px; color: #9E9E9E; margin: 16px 0 0; }
  `]
})
export class LoginComponent implements OnInit {
  private authService = inject(AuthService);
  private authStore = inject(AuthStore);
  private router = inject(Router);

  loading = signal(false);
  error = signal<string | null>(null);
  showPassword = signal(false);

  email = '';
  password = '';

  ngOnInit(): void {
    if (this.authStore.isAuthenticated()) {
      this.router.navigate(['/fleet']);
    }
  }

  async login(): Promise<void> {
    if (!this.email || !this.password || this.loading()) return;

    this.loading.set(true);
    this.error.set(null);

    try {
      await this.authService.login(this.email, this.password);
      await this.router.navigate(['/fleet']);
    } catch {
      this.error.set('Credenciales incorrectas. Verifica tu correo y contraseña.');
    } finally {
      this.loading.set(false);
    }
  }
}
