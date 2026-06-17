import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import type { HubConnection } from '@microsoft/signalr';
import { AuthService } from './auth.service';
import { ChamadoTIComunicação } from './models';

@Injectable({ providedIn: 'root' })
export class ChatAlertService {
  readonly message = signal<ChamadoTIComunicação | null>(null);
  private connection: HubConnection | null = null;

  constructor(
    private readonly auth: AuthService,
    private readonly http: HttpClient,
    private readonly router: Router,
  ) {}

  async start(): Promise<void> {
    if (this.connection || !this.auth.token()) {
      return;
    }

    const signalR = await import('@microsoft/signalr');
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/chamados-ti-chat', { accessTokenFactory: () => this.auth.token() ?? '' })
      .withAutomaticReconnect()
      .build();

    connection.on('NovaMensagemChamado', (message: ChamadoTIComunicação) => this.message.set(message));

    try {
      await connection.start();
      this.connection = connection;
    } catch {
      this.connection = null;
    }
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.message.set(null);
    await connection?.stop().catch(() => undefined);
  }

  dismiss(): void {
    this.markRead();
    this.message.set(null);
  }

  respond(): void {
    const message = this.message();
    this.dismiss();
    void this.router.navigate(['/ti'], {
      queryParams: message ? { chamadoId: message.chamadoTIId, chat: 1 } : undefined,
    });
  }

  private markRead(): void {
    const message = this.message();
    if (!message) {
      return;
    }

    this.http
      .post(`/api/chamadosti/${message.chamadoTIId}/comunicacoes/${message.id}/lida`, {})
      .subscribe({ error: () => undefined });
  }
}
