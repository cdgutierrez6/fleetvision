import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from './auth.store';

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  tokenType: string;
}

interface MeResponse {
  id: string;
  tenantId: string | null;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private authStore = inject(AuthStore);

  async login(email: string, password: string): Promise<void> {
    const token = await firstValueFrom(
      this.http.post<LoginResponse>('/api/identity/auth/login', { email, password })
    );

    const me = await firstValueFrom(
      this.http.get<MeResponse>('/api/identity/auth/me', {
        headers: new HttpHeaders({ Authorization: `Bearer ${token.accessToken}` }),
      })
    );

    this.authStore.setSession(token.accessToken, {
      id: me.id,
      email: me.email,
      name: `${me.firstName} ${me.lastName}`.trim(),
      tenantId: me.tenantId ?? '',
      tenantName: 'FleetVision',
    });
  }

  logout(): void {
    this.authStore.clearSession();
  }

  isLoggedIn(): boolean {
    return this.authStore.isAuthenticated();
  }
}
