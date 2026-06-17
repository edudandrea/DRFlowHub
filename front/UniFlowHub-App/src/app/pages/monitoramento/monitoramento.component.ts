import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { ChamadosTIService } from '../../core/chamados-ti.service';
import { ChamadoTIPayload } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';

type MonitorStatus = 'online' | 'offline' | 'testando' | 'pendente';
type MonitorTipo = 'Link de internet' | 'Firewall' | 'Conexao';

interface MonitorItem {
  id: number;
  nome: string;
  tipo: MonitorTipo;
  alvo: string;
  intervaloSegundos: number;
  status: MonitorStatus;
  ultimaConsulta?: string;
  tempoRespostaMs?: number;
  erro?: string;
  incidenteChamadoId?: number;
  incidenteCriadoEm?: string;
}

interface MonitoramentoTesteResponse {
  online: boolean;
  status: string;
  mensagem: string;
  tempoRespostaMs: number;
  protocolo: string;
}

const STORAGE_KEY = 'uniflowhub.ti.monitoramento';
const DEFAULT_INTERVAL_SECONDS = 60;

@Component({
  selector: 'app-monitoramento',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './monitoramento.component.html',
  styleUrls: ['./monitoramento.component.css'],
})
export class MonitoramentoComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  private readonly toastr = inject(ToastrService);
  private readonly auth = inject(AuthService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly chamadosTIService = inject(ChamadosTIService);
  private readonly incidentCreationInProgress = new Set<number>();
  readonly theme = inject(ThemeService);

  readonly itens = signal<MonitorItem[]>([]);
  readonly testingAll = signal(false);
  readonly profileMenuOpen = signal(false);
  readonly editingItem = signal<MonitorItem | null>(null);
  readonly onlineCount = computed(() => this.itens().filter((item) => item.status === 'online').length);
  readonly offlineCount = computed(() => this.itens().filter((item) => item.status === 'offline').length);
  readonly pendingCount = computed(() => this.itens().filter((item) => item.status === 'pendente' || item.status === 'testando').length);
  readonly user = computed(() => this.auth.user());

  readonly form = this.fb.nonNullable.group({
    nome: ['', Validators.required],
    tipo: ['Link de internet' as MonitorTipo, Validators.required],
    alvo: ['', Validators.required],
    intervaloSegundos: [DEFAULT_INTERVAL_SECONDS, [Validators.required, Validators.min(10)]],
  });

  readonly editForm = this.fb.nonNullable.group({
    nome: ['', Validators.required],
    tipo: ['Link de internet' as MonitorTipo, Validators.required],
    alvo: ['', Validators.required],
    intervaloSegundos: [DEFAULT_INTERVAL_SECONDS, [Validators.required, Validators.min(10)]],
  });

  private timers = new Map<number, ReturnType<typeof setInterval>>();

  ngOnInit(): void {
    this.itens.set(this.loadItems());
    this.scheduleAll();
    if (this.itens().length) {
      void this.testAll();
    }
  }

  ngOnDestroy(): void {
    this.clearTimers();
  }

  goHome(): void {
    void this.router.navigateByUrl('/hub');
  }

  editProfile(): void {
    this.profileMenuOpen.set(false);
    this.profileFlow.editProfile();
  }

  changePassword(): void {
    this.profileMenuOpen.set(false);
    this.profileFlow.changePassword();
  }

  logout(): void {
    this.auth.logout();
  }

  addItem(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toastr.warning('Informe nome, IP ou URL e o intervalo de consulta.', 'Monitoramento');
      return;
    }

    const value = this.form.getRawValue();
    const item: MonitorItem = {
      id: Date.now(),
      nome: value.nome.trim(),
      tipo: value.tipo,
      alvo: value.alvo.trim(),
      intervaloSegundos: Math.max(10, Number(value.intervaloSegundos) || DEFAULT_INTERVAL_SECONDS),
      status: 'pendente',
    };

    this.itens.set([item, ...this.itens()]);
    this.saveItems();
    this.scheduleItem(item);
    this.form.reset({
      nome: '',
      tipo: 'Link de internet',
      alvo: '',
      intervaloSegundos: DEFAULT_INTERVAL_SECONDS,
    });
    void this.testItem(item);
  }

  removeItem(item: MonitorItem): void {
    const timer = this.timers.get(item.id);
    if (timer) {
      clearInterval(timer);
      this.timers.delete(item.id);
    }
    this.itens.set(this.itens().filter((current) => current.id !== item.id));
    this.saveItems();
  }

  openEditModal(item: MonitorItem): void {
    this.editingItem.set(item);
    this.editForm.reset({
      nome: item.nome,
      tipo: item.tipo,
      alvo: item.alvo,
      intervaloSegundos: item.intervaloSegundos,
    });
  }

  closeEditModal(): void {
    this.editingItem.set(null);
  }

  saveEdit(): void {
    const item = this.editingItem();
    if (!item) {
      return;
    }

    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      this.toastr.warning('Confira os dados do monitoramento antes de salvar.', 'Monitoramento');
      return;
    }

    const value = this.editForm.getRawValue();
    const updated: MonitorItem = {
      ...item,
      nome: value.nome.trim(),
      tipo: value.tipo,
      alvo: value.alvo.trim(),
      intervaloSegundos: Math.max(10, Number(value.intervaloSegundos) || DEFAULT_INTERVAL_SECONDS),
      status: 'pendente',
      ultimaConsulta: undefined,
      tempoRespostaMs: undefined,
      erro: '',
    };

    this.itens.set(this.itens().map((current) => current.id === item.id ? updated : current));
    this.saveItems();
    this.rescheduleItem(updated);
    this.closeEditModal();
    void this.testItem(updated);
  }

  async testAll(): Promise<void> {
    if (this.testingAll()) {
      return;
    }

    this.testingAll.set(true);
    await Promise.all(this.itens().map((item) => this.testItem(item)));
    this.testingAll.set(false);
  }

  async testItem(item: MonitorItem): Promise<void> {
    this.patchItem(item.id, { status: 'testando', erro: '' });

    const previousStatus = item.status;

    try {
      const response = await this.probe(item.alvo);
      if (!response.online) {
        throw new Error(response.mensagem || 'Sem resposta no tempo configurado');
      }

      this.patchItem(item.id, {
        status: 'online',
        ultimaConsulta: new Date().toISOString(),
        tempoRespostaMs: response.tempoRespostaMs,
        erro: '',
        incidenteChamadoId: undefined,
        incidenteCriadoEm: undefined,
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Sem resposta no tempo configurado';
      this.patchItem(item.id, {
        status: 'offline',
        ultimaConsulta: new Date().toISOString(),
        tempoRespostaMs: undefined,
        erro: message,
      });

      if (previousStatus !== 'offline' || !item.incidenteChamadoId) {
        await this.createIncidentTicket(item, message);
      }
    }
  }

  statusLabel(status: MonitorStatus): string {
    const labels: Record<MonitorStatus, string> = {
      online: 'Online',
      offline: 'Offline',
      testando: 'Testando',
      pendente: 'Pendente',
    };
    return labels[status];
  }

  formatTarget(value: string): string {
    return this.toProbeUrl(value).replace(/^https?:\/\//, '');
  }

  private probe(value: string): Promise<MonitoramentoTesteResponse> {
    return new Promise((resolve, reject) => {
      this.http.post<MonitoramentoTesteResponse>('/api/monitoramento/testar', { alvo: value }).subscribe({
        next: resolve,
        error: (error) => reject(new Error(error?.error || 'Nao foi possivel testar o alvo pelo servidor.')),
      });
    });
  }

  private async createIncidentTicket(item: MonitorItem, errorMessage: string): Promise<void> {
    if (this.incidentCreationInProgress.has(item.id)) {
      return;
    }

    this.incidentCreationInProgress.add(item.id);
    try {
      const currentUser = this.auth.user();
      const payload: ChamadoTIPayload = {
        titulo: `Link offline: ${item.nome}`,
        categoria: item.tipo === 'Link de internet' ? 'Rede' : 'Suporte',
        descricao: `O monitoramento automático detectou que o recurso "${item.nome}" (${item.tipo}) ficou offline em ${new Date().toLocaleString('pt-BR')}. Alvo: ${item.alvo}. Erro: ${errorMessage}`,
        solicitante: currentUser?.nome ?? 'Monitoramento',
        unidade: currentUser?.unidadeNome ?? '',
        departamento: 'TI',
        prioridade: 'Alta',
        status: 'Aberto',
        responsavel: '',
        acessoRemotoUrl: '',
        acessoRemotoSenha: '',
        equipamentoNome: item.nome,
        equipamentoIp: item.alvo,
        equipamentoSistemaOperacional: '',
        observacoes: `Incidente detectado em ${new Date().toLocaleString('pt-BR')}. Erro: ${errorMessage}`,
        dataAprovacao: null,
        aprovada: null,
        observacoesAprovacao: '',
        aprovacaoPendente: false,
        userid: 0,
      };

      const chamado = await firstValueFrom(this.chamadosTIService.create(payload));
      this.patchItem(item.id, {
        incidenteChamadoId: chamado.id,
        incidenteCriadoEm: new Date().toISOString(),
      });
      this.toastr.warning(
        `O link "${item.nome}" ficou offline e o chamado #${chamado.id} foi aberto automaticamente.`,
        'Monitoramento offline',
      );
    } catch {
      this.toastr.error(
        `Falha ao abrir chamado automático para o link "${item.nome}".`,
        'Monitoramento offline',
      );
    } finally {
      this.incidentCreationInProgress.delete(item.id);
    }
  }

  private toProbeUrl(value: string): string {
    const target = value.trim();
    if (/^https?:\/\//i.test(target)) {
      return target;
    }

    return `http://${target}`;
  }

  private scheduleAll(): void {
    this.clearTimers();
    this.itens().forEach((item) => this.scheduleItem(item));
  }

  private scheduleItem(item: MonitorItem): void {
    const timer = setInterval(() => {
      const current = this.itens().find((candidate) => candidate.id === item.id);
      if (current) {
        void this.testItem(current);
      }
    }, Math.max(10, item.intervaloSegundos) * 1000);
    this.timers.set(item.id, timer);
  }

  private rescheduleItem(item: MonitorItem): void {
    const timer = this.timers.get(item.id);
    if (timer) {
      clearInterval(timer);
      this.timers.delete(item.id);
    }
    this.scheduleItem(item);
  }

  private clearTimers(): void {
    for (const timer of this.timers.values()) {
      clearInterval(timer);
    }
    this.timers.clear();
  }

  private patchItem(id: number, patch: Partial<MonitorItem>): void {
    this.itens.set(this.itens().map((item) => item.id === id ? { ...item, ...patch } : item));
    this.saveItems();
  }

  private loadItems(): MonitorItem[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return [];
      }

      return (JSON.parse(raw) as MonitorItem[]).map((item) => ({
        ...item,
        intervaloSegundos: Math.max(10, Number(item.intervaloSegundos) || DEFAULT_INTERVAL_SECONDS),
        status: item.status || 'pendente',
      }));
    } catch {
      return [];
    }
  }

  private saveItems(): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(this.itens()));
  }
}
