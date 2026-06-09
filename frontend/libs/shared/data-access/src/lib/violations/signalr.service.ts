import { Injectable, inject } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { AuthStore } from '../auth/auth.store';
import { ViolationsStore } from './violations.store';
import { ViolationEvent } from '@fleetvision/shared/models';

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private authStore = inject(AuthStore);
  private violationsStore = inject(ViolationsStore);
  private connection: signalR.HubConnection | null = null;
  private readonly hubUrl = '/hubs/violations';

  connect(): void {
    if (this.connection?.state === signalR.HubConnectionState.Connected) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => this.authStore.accessToken() ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('ViolationDetected', (event: ViolationEvent) => {
      this.violationsStore.append(event);
    });

    this.connection.onreconnecting(() => {
      this.violationsStore.setConnected(false);
    });

    this.connection.onreconnected(() => {
      this.violationsStore.setConnected(true);
      this.joinTenantGroup();
    });

    this.connection.onclose(() => {
      this.violationsStore.setConnected(false);
    });

    this.connection
      .start()
      .then(() => {
        this.violationsStore.setConnected(true);
        this.joinTenantGroup();
      })
      .catch(err => console.error('[SignalR] Connection failed:', err));
  }

  disconnect(): void {
    this.connection?.stop().catch(err =>
      console.error('[SignalR] Disconnect error:', err)
    );
  }

  private joinTenantGroup(): void {
    const tenantId = this.authStore.tenantId();
    if (!tenantId || !this.connection) return;
    this.connection
      .invoke('JoinTenantGroup', tenantId)
      .catch(err => console.error('[SignalR] JoinTenantGroup error:', err));
  }
}
