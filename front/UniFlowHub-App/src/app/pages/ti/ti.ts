import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, TemplateRef, ViewChild, computed, inject, signal } from '@angular/core';
import { DatePipe, isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { NgxSpinnerService } from 'ngx-spinner';
import { HttpErrorResponse } from '@angular/common/http';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { ActivatedRoute, Router } from '@angular/router';
import type { HubConnection } from '@microsoft/signalr';
import { AuthService } from '../../core/auth.service';
import { BaseConhecimentoTIService } from '../../core/base-conhecimento-ti.service';
import { EquipamentosTIService } from '../../core/equipamentos-ti.service';
import { BaseConhecimentoTI, EquipamentoTI } from '../../core/models';
import { ChamadosTIService } from '../../core/chamados-ti.service';
import { ChamadoTI, ChamadoTIComunicação, ChamadoTIPayload, Unidade, User } from '../../core/models';
import { ThemeService } from '../../core/theme.service';
import { TiAssistantService } from '../../core/ti-assistant.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { UnidadesService } from '../../core/unidades.service';

const ALLOWED_ATTACHMENT_TYPES = [
  'image/jpeg',
  'image/png',
  'image/gif',
  'image/webp',
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
];
const ALLOWED_ATTACHMENT_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.pdf', '.doc', '.docx'];
const MONITORING_STORAGE_KEY = 'uniflowhub.ti.monitoramento';
type TiTab = 'pendentes' | 'meus' | 'todos' | 'concluidos';
type TiSortField = 'id' | 'titulo' | 'solicitante' | 'unidade' | 'departamento' | 'prioridade' | 'status' | 'dataAbertura';
type MonitorStatus = 'online' | 'offline' | 'testando' | 'pendente';
type MetricFilter = 'none' | 'abertos' | 'sla-cumprido' | 'sla-vencido' | 'em-atendimento' | 'respondidos';

interface TicketColumn {
  key: string;
  title: string;
  subtitle: string;
  items: ChamadoTI[];
}

interface TicketTimelineEvent {
  time: string;
  label: string;
}

interface MonitorItem {
  id: number;
  nome: string;
  tipo: string;
  alvo: string;
  intervaloSegundos: number;
  status: MonitorStatus;
  ultimaConsulta?: string;
  tempoRespostaMs?: number;
  erro?: string;
}

interface TicketMovementAlert {
  id: number;
  ticketId: number;
  title: string;
  date: string;
}

@Component({
  selector: 'app-ti',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './ti.html',
  styleUrl: './ti.scss',
})
export class TiPage implements OnInit, AfterViewInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(ChamadosTIService);
  private readonly baseConhecimentoService = inject(BaseConhecimentoTIService);
  private readonly equipamentosService = inject(EquipamentosTIService);
  private readonly tiAssistantService = inject(TiAssistantService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly modalService = inject(BsModalService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly chamados = signal<ChamadoTI[]>([]);
  readonly conhecimentos = signal<BaseConhecimentoTI[]>([]);
  readonly equipamentos = signal<EquipamentoTI[]>([]);
  readonly monitoramentoLinks = signal<MonitorItem[]>([]);
  readonly unidades = signal<Unidade[]>([]);
  readonly responsaveis = signal<User[]>([]);
  readonly selected = signal<ChamadoTI | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly updating = signal(false);
  readonly selectedFileName = signal('');
  readonly selectedFilePreviewUrl = signal<SafeResourceUrl | null>(null);
  readonly selectedFilePreviewType = signal<'image' | 'pdf' | 'download' | ''>('');
  readonly attachmentPreviewUrl = signal<SafeResourceUrl | null>(null);
  readonly attachmentPreviewType = signal<'image' | 'pdf' | 'download' | ''>('');
  readonly profileMenuOpen = signal(false);
  readonly activeTab = signal<TiTab>('pendentes');
  readonly detailTab = signal<'detalhes' | 'comunicacao'>('detalhes');
  readonly filterTerm = signal('');
  readonly responsibleFilter = signal('');
  readonly metricFilter = signal<MetricFilter>('none');
  readonly dateFrom = signal('');
  readonly dateTo = signal('');
  readonly reportDateFrom = signal('');
  readonly reportDateTo = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly sortField = signal<TiSortField>('id');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');
  readonly comunicacoes = signal<ChamadoTIComunicação[]>([]);
  readonly movementAlerts = signal<TicketMovementAlert[]>([]);
  readonly loadingComunicacoes = signal(false);
  readonly sendingMessage = signal(false);
  readonly closing = signal(false);
  readonly reopening = signal(false);
  readonly rating = signal(false);
  readonly generatingReport = signal(false);
  readonly assistantOpen = signal(false);
  readonly assistantQuestion = signal('');
  readonly assistantSearching = signal(false);
  readonly assistantMessages = signal<{ role: 'assistant' | 'user'; text: string; articleId?: number; suggestOpenTicket?: boolean; category?: string; priority?: string; technician?: string }[]>([]);
  readonly createdTicketNumber = signal<number | null>(null);
  readonly user = computed(() => this.auth.user());
  readonly canManage = computed(() => this.auth.hasAccess('ti-admin'));
  readonly abertos = computed(() => this.chamados().filter((item) => item.status === 'Aberto').length);
  readonly emAtendimento = computed(() => this.chamados().filter((item) => !this.isConcluido(item) && !!item.responsavel?.trim()).length);
  readonly pendentes = computed(() => this.chamados().filter((item) => !this.isConcluido(item) && !item.responsavel?.trim()).length);
  readonly meusChamados = computed(() => this.chamados().filter((item) => !this.isConcluido(item) && this.isMine(item)).length);
  readonly concluidos = computed(() => this.chamados().filter((item) => this.isConcluido(item)).length);
  readonly avaliacoesPendentes = computed(
    () => this.chamados().filter((item) => this.canApprove(item) || this.canEvaluate(item)).length,
  );
  readonly chamadosRespondidos = computed(() => this.movementAlerts().length);
  readonly chamadosPorSetor = computed(() => {
    const grouped = this.chamados().reduce<Record<string, number>>((acc, item) => {
      const key = item.departamento?.trim() || 'Sem setor';
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});

    return Object.entries(grouped)
      .sort((a, b) => b[1] - a[1])
      .slice(0, 3)
      .map(([setor, total]) => `${setor}: ${total}`)
      .join(' | ') || 'Sem dados';
  });
  readonly slaCumprido = computed(() => this.chamados().filter((item) => this.isSlaMet(item)).length);
  readonly slaVencido = computed(() => this.chamados().filter((item) => this.isSlaExpired(item)).length);
  readonly equipamentosPorUnidade = computed(() => {
    const grouped = this.equipamentos().reduce<Record<string, number>>((acc, item) => {
      const key = item.destino?.trim() || item.origem?.trim() || 'Sem unidade';
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});

    const first = Object.entries(grouped).sort((a, b) => b[1] - a[1])[0];
    return first ? `${first[0]}: ${first[1]}` : 'Sem equipamentos';
  });
  readonly disponibilidadeLinks = computed(() => {
    const links = this.monitoramentoLinks();
    if (links.length === 0) {
      return '0%';
    }

    const online = links.filter((item) => item.status === 'online').length;
    const disponibilidade = (online / links.length) * 100;
    return `${disponibilidade.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;
  });
  readonly filteredChamados = computed(() => {
    let items = this.chamados();

    if (this.canManage()) {
      if (this.activeTab() === 'concluidos') {
        items = this.sortConcluidos(items.filter((item) => this.isConcluido(item)));
      } else {
        items = items.filter((item) => !this.isConcluido(item));
      }

      if (this.activeTab() === 'pendentes') {
        items = items.filter((item) => !item.responsavel?.trim());
      }

      if (this.activeTab() === 'meus') {
        items = items.filter((item) => this.isMine(item));
      }
    }

    items = this.applyMetricFilter(items);

    const term = this.normalize(this.filterTerm());
    const responsible = this.normalize(this.responsibleFilter());
    const from = this.parseDateFilter(this.dateFrom(), false);
    const to = this.parseDateFilter(this.dateTo(), true);
    const isConcluidosTab = this.canManage() && this.activeTab() === 'concluidos';

    const filtered = items.filter((item) => {
      const itemDate = this.getFilterDate(item, isConcluidosTab);
      const matchesTerm = !term || this.matchesFilter(item, term);
      const matchesResponsible = !responsible || this.normalize(item.responsavel || 'Sem responsavel') === responsible;
      const matchesFrom = !from || itemDate >= from;
      const matchesTo = !to || itemDate <= to;

      return matchesTerm && matchesResponsible && matchesFrom && matchesTo;
    });

    return this.sortItems(filtered);
  });
  readonly ticketColumns = computed<TicketColumn[]>(() => {
    const items = this.filteredChamados().slice((this.safePage() - 1) * this.pageSize(), this.safePage() * this.pageSize());
    const columns = [
      {
        key: 'abertos',
        title: 'Abertos',
        items: items.filter((item) => !this.isConcluido(item) && this.normalize(item.status).includes('aberto')),
      },
      {
        key: 'andamento',
        title: 'Em andamento',
        items: items.filter((item) => !this.isConcluido(item) && !this.isAwaitingUser(item) && !this.normalize(item.status).includes('aberto')),
      },
      {
        key: 'aguardando',
        title: 'Aguardando usuario',
        items: items.filter((item) => !this.isConcluido(item) && this.isAwaitingUser(item)),
      },
      {
        key: 'concluidos',
        title: 'Concluídos',
        items: items.filter((item) => this.isConcluido(item)),
      },
    ];

    return columns.map((column) => ({
      ...column,
      subtitle: `${column.items.length} chamado(s)`,
    }));
  });
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filteredChamados().length / this.pageSize())));
  readonly pagedChamados = computed(() => this.filteredChamados().slice((this.safePage() - 1) * this.pageSize(), this.safePage() * this.pageSize()));
  private selectedAttachment: File | null = null;
  private selectedAttachmentObjectUrl = '';
  private attachmentObjectUrl = '';
  private ticketModalRef?: BsModalRef;
  private createTicketModalRef?: BsModalRef;
  private satisfactionModalRef?: BsModalRef;
  private communicationRefreshId: ReturnType<typeof setInterval> | null = null;
  private ticketMovementRefreshId: ReturnType<typeof setInterval> | null = null;
  private chatConnection: HubConnection | null = null;
  private chatChamadoId: number | null = null;
  private knownTicketMovements = new Map<number, string>();
  private movementAlertSequence = 0;

  @ViewChild('anexoInput') private anexoInput?: ElementRef<HTMLInputElement>;
  @ViewChild('ticketModalTemplate') private ticketModalTemplate?: TemplateRef<void>;
  @ViewChild('createTicketModalTemplate') private createTicketModalTemplate?: TemplateRef<void>;
  @ViewChild('approvalModalTemplate') private approvalModalTemplate?: TemplateRef<void>;
  @ViewChild('satisfactionModalTemplate') private satisfactionModalTemplate?: TemplateRef<void>;

  private approvalModalRef?: BsModalRef;
  readonly approvalForm = this.fb.nonNullable.group({
    observacoesAprovacao: [''],
  });
  readonly approving = signal(false);

  readonly form = this.fb.nonNullable.group({
    titulo: ['', Validators.required],
    categoria: ['Suporte', Validators.required],
    descricao: ['', Validators.required],
    solicitante: ['', Validators.required],
    unidade: ['', Validators.required],
    departamento: ['', Validators.required],
    prioridade: ['Media', Validators.required],
    status: ['Aberto', Validators.required],
    responsavel: [''],
    acessoRemotoUrl: [''],
    acessoRemotoSenha: [''],
    equipamentoNome: [''],
    equipamentoIp: [''],
    equipamentoSistemaOperacional: [''],
    observacoes: [''],
  });

  readonly adminForm = this.fb.nonNullable.group({
    titulo: ['', Validators.required],
    categoria: ['', Validators.required],
    descricao: ['', Validators.required],
    solicitante: ['', Validators.required],
    unidade: ['', Validators.required],
    departamento: ['', Validators.required],
    prioridade: ['', Validators.required],
    status: ['', Validators.required],
    responsavel: [''],
    acessoRemotoUrl: [''],
    acessoRemotoSenha: [''],
    equipamentoNome: [''],
    equipamentoIp: [''],
    equipamentoSistemaOperacional: [''],
    observacoes: [''],
  });

  readonly messageForm = this.fb.nonNullable.group({
    mensagem: ['', Validators.required],
  });

  readonly closeForm = this.fb.nonNullable.group({
    observacoesEncerramento: ['', Validators.required],
  });

  readonly satisfactionForm = this.fb.nonNullable.group({
    nota: [0, [Validators.required, Validators.min(1), Validators.max(5)]],
    comentario: [''],
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    const user = this.user();
    if (user) {
      this.form.patchValue({
        solicitante: user.nome,
        unidade: this.getUserUnidadeName(),
        departamento: user.departamento,
        equipamentoNome: this.getBrowserComputerName(),
        equipamentoSistemaOperacional: this.getOperatingSystem(),
      });
    }

    this.loadUnidades();
    this.loadMonitoringLinks();
    this.loadConhecimentos();
    this.loadEquipamentos();
    this.load();
    if (this.canManage()) {
      this.loadResponsaveis();
      this.startTicketMovementRefresh();
    }
  }

  ngAfterViewInit(): void {
    if (!this.isBrowser) {
      return;
    }

    setTimeout(() => this.openQrRequestedTicket());
  }

  ngOnDestroy(): void {
    this.stopCommunicationRefresh();
    this.stopTicketMovementRefresh();
    void this.disconnectChat();
  }

  loadUnidades(): void {
    this.unidadesService.list().subscribe({
      next: (unidades) => {
        this.unidades.set(unidades);
        const userUnidade = this.getUserUnidadeName();
        if (userUnidade) {
          this.form.patchValue({ unidade: userUnidade });
        }
      },
      error: () => this.toastr.error('Não foi possível carregar as unidades.', 'Erro'),
    });
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.service.list().subscribe({
      next: (items) => {
        this.chamados.set(items);
        this.loading.set(false);
        void this.spinner.hide();
        const firstVisible = this.filteredChamados()[0] ?? items[0];
        if (!this.selected() && firstVisible) {
          this.select(firstVisible);
        }
        this.openRequestedChat(items);
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar os chamados de TI.', 'Erro');
      },
    });
  }

  loadResponsaveis(): void {
    this.auth.listAdministradores().subscribe({
      next: (users) => this.responsaveis.set(users),
      error: () => this.toastr.error('Não foi possível carregar os responsáveis.', 'Erro'),
    });
  }

  loadConhecimentos(): void {
    this.baseConhecimentoService.list().subscribe({
      next: (items) => this.conhecimentos.set(items),
      error: () => this.toastr.error('Não foi possível carregar a base de conhecimento de TI.', 'TI Assistant'),
    });
  }

  loadEquipamentos(): void {
    this.equipamentosService.list().subscribe({
      next: (items) => this.equipamentos.set(items),
      error: () => this.toastr.error('Não foi possível carregar os equipamentos de TI.', 'TI'),
    });
  }

  loadMonitoringLinks(): void {
    try {
      const raw = localStorage.getItem(MONITORING_STORAGE_KEY);
      this.monitoramentoLinks.set(raw ? JSON.parse(raw) as MonitorItem[] : []);
    } catch {
      this.monitoramentoLinks.set([]);
    }
  }

  openTiAssistant(): void {
    const title = this.form.controls.titulo.value.trim();
    const description = this.form.controls.descricao.value?.trim() ?? '';
    const question = this.assistantQuestion().trim();
    const sourceText = [title, description, question].filter(Boolean).join(' ');
    const analysis = this.buildAssistantAnalysis(sourceText);

    const messages = [
      {
        role: 'assistant' as const,
        text: analysis.initialResponse,
        category: analysis.category,
        priority: analysis.priority,
        technician: analysis.technician,
        articleId: analysis.article?.id,
        suggestOpenTicket: analysis.suggestOpenTicket,
      },
    ];

    this.assistantMessages.set(messages);
    this.assistantQuestion.set('');
    this.assistantOpen.set(true);
  }

  closeTiAssistant(): void {
    this.assistantOpen.set(false);
  }

  openTicketFromAssistant(): void {
    if (this.createTicketModalTemplate) {
      this.assistantOpen.set(false);
      this.openCreateTicketModal(this.createTicketModalTemplate);
    }
  }

  sendAssistantQuestion(): void {
    const question = this.assistantQuestion().trim();
    if (!question || this.assistantSearching()) {
      return;
    }

    const title = this.form.controls.titulo.value.trim();
    const description = this.form.controls.descricao.value?.trim() ?? '';
    const contextText = [title, description, question].filter(Boolean).join(' ');
    const analysis = this.buildAssistantAnalysis(contextText);

    this.assistantSearching.set(true);
    this.assistantMessages.set([
      ...this.assistantMessages(),
      { role: 'user', text: question },
    ]);
    this.assistantQuestion.set('');

    if (analysis.article) {
      this.assistantMessages.set([
        ...this.assistantMessages(),
        {
          role: 'assistant',
          text: analysis.initialResponse,
          category: analysis.category,
          priority: analysis.priority,
          technician: analysis.technician,
          articleId: analysis.article.id,
          suggestOpenTicket: false,
        },
      ]);
      this.assistantSearching.set(false);
      return;
    }

    this.tiAssistantService.searchGlobal(title, question, this.buildAssistantLocalContext(question)).subscribe({
      next: (response) => {
        const answer = response.configured && response.answer?.trim()
          ? response.answer.trim()
          : this.buildAssistantFallbackAnswer(question);
        this.assistantMessages.set([
          ...this.assistantMessages(),
          {
            role: 'assistant',
            text: `${analysis.initialResponse}\n\n${answer}`,
            category: analysis.category,
            priority: analysis.priority,
            technician: analysis.technician,
            suggestOpenTicket: !response.configured || !response.answer?.trim(),
          },
        ]);
        this.assistantSearching.set(false);
      },
      error: () => {
        this.assistantMessages.set([
          ...this.assistantMessages(),
          {
            role: 'assistant',
            text: `${analysis.initialResponse}\n\n${this.buildAssistantFallbackAnswer(question)}`,
            category: analysis.category,
            priority: analysis.priority,
            technician: analysis.technician,
            suggestOpenTicket: true,
          },
        ]);
        this.assistantSearching.set(false);
      },
    });
  }

  private buildAssistantFallbackAnswer(question: string): string {
    const matches = this.findKnowledgeMatches(`${this.form.controls.titulo.value} ${question}`);
    return matches.length > 0
      ? `Achei uma referencia na base: ${matches[0].titulo}. ${matches[0].descricao}`
      : this.buildAssistantGuidance(question);
  }

  private buildAssistantAnalysis(text: string): {
    category: string;
    priority: string;
    technician: string;
    initialResponse: string;
    article?: BaseConhecimentoTI;
    suggestOpenTicket: boolean;
  } {
    const normalized = this.normalize(text);
    const category = this.detectIssueCategory(normalized);
    const priority = this.detectIssuePriority(normalized);
    const technician = this.suggestTechnician();
    const matches = this.findKnowledgeMatches(text);

    if (matches.length > 0) {
      return {
        category,
        priority,
        technician,
        article: matches[0],
        suggestOpenTicket: false,
        initialResponse: `Sugestão: categoria ${category}, prioridade ${priority} e técnico ${technician}. Encontrei um artigo na base de conhecimento: ${matches[0].titulo}. ${matches[0].descricao}`,
      };
    }

    return {
      category,
      priority,
      technician,
      suggestOpenTicket: true,
      initialResponse: `Sugestão: categoria ${category}, prioridade ${priority} e técnico ${technician}. Não encontrei um artigo semelhante na base de conhecimento.`, 
    };
  }

  private detectIssueCategory(text: string): string {
    if (/(sistema|apolo|erp|software|aplicacao|aplicativo|portal|app|oracle)/.test(text)) {
      return 'Sistema';
    }
    if (/(internet|rede|wifi|wi-?fi|vpn|proxy|lan|cabo|conexao)/.test(text)) {
      return 'Rede';
    }
    if (/(impressora|scanner|impressoras|scanners|multifuncional|equipamento)/.test(text)) {
      return 'Equipamento';
    }
    if (/(acesso|login|senha|autenticacao|autenticação|bloqueado)/.test(text)) {
      return 'Acesso';
    }
    return 'Suporte';
  }

  private detectIssuePriority(text: string): string {
    if (/(nao consigo|não consigo|minha|parado|crítico|critico|urgente|impossivel|impossível|falha geral|down|indisponivel|indisponível)/.test(text)) {
      return 'Alta';
    }
    if (/(lento|lentidao|lentidão|instavel|instável|intermitente|erro|falha)/.test(text)) {
      return 'Media';
    }
    return 'Baixa';
  }

  private suggestTechnician(): string {
    const available = this.responsaveis();
    if (available.length === 0) {
      return 'técnico de TI disponível';
    }

    const preferred = available.find((user) => this.normalize(user.role).includes('ti'))
      ?? available[0];

    return preferred.nome || preferred.email || 'técnico de TI disponível';
  }

  showAssistantButton(): boolean {
    return this.form.controls.titulo.value.trim().length >= 3;
  }

  setTab(tab: TiTab): void {
    this.activeTab.set(tab);
    this.metricFilter.set('none');
    this.page.set(1);
    this.clearAttachmentPreview();
    const first = this.filteredChamados()[0] ?? null;
    this.selected.set(null);
    this.comunicacoes.set([]);
    if (first) {
      this.select(first);
    }
  }

  setSort(field: TiSortField): void {
    if (this.sortField() === field) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortField.set(field);
      this.sortDirection.set(field === 'id' || field === 'dataAbertura' ? 'desc' : 'asc');
    }
    this.page.set(1);
  }

  previousPage(): void {
    this.page.set(Math.max(1, this.safePage() - 1));
  }

  nextPage(): void {
    this.page.set(Math.min(this.totalPages(), this.safePage() + 1));
  }

  setFilter(value: string): void {
    this.filterTerm.set(value);
    this.syncSelectionWithFilters();
  }

  setResponsibleFilter(value: string): void {
    this.responsibleFilter.set(value);
    this.page.set(1);
    this.syncSelectionWithFilters();
  }

  clearQueueFilters(): void {
    this.filterTerm.set('');
    this.responsibleFilter.set('');
    this.metricFilter.set('none');
    this.dateFrom.set('');
    this.dateTo.set('');
    this.page.set(1);
    this.syncSelectionWithFilters();
  }

  private syncSelectionWithFilters(): void {
    const selected = this.selected();
    if (selected && !this.filteredChamados().some((item) => item.id === selected.id)) {
      const first = this.filteredChamados()[0] ?? null;
      this.selected.set(null);
      this.comunicacoes.set([]);
      if (first) {
        this.select(first);
      }
    }
  }

  clearDateFilters(): void {
    this.dateFrom.set('');
    this.dateTo.set('');
    this.page.set(1);
    this.syncSelectionWithFilters();
  }

  setMetricFilter(filter: MetricFilter): void {
    const nextFilter = this.metricFilter() === filter ? 'none' : filter;
    this.metricFilter.set(nextFilter);
    if (nextFilter === 'sla-cumprido') {
      this.activeTab.set('concluidos');
    } else if (nextFilter !== 'none') {
      this.activeTab.set('todos');
    }
    this.page.set(1);
    this.syncSelectionWithFilters();
  }

  metricTrend(filter: MetricFilter): string {
    if (filter === 'none') {
      return '';
    }

    const now = Date.now();
    const week = 7 * 24 * 60 * 60 * 1000;
    const current = this.countMetricInRange(filter, now - week, now);
    const previous = this.countMetricInRange(filter, now - week * 2, now - week);
    const change = previous === 0 ? (current > 0 ? 100 : 0) : Math.round(((current - previous) / previous) * 100);
    const direction = change >= 0 ? 'subiu' : 'caiu';
    return `${direction} ${Math.abs(change)}% esta semana`;
  }

  metricTrendClass(filter: MetricFilter): string {
    if (filter === 'none') {
      return 'neutral';
    }

    const now = Date.now();
    const week = 7 * 24 * 60 * 60 * 1000;
    const current = this.countMetricInRange(filter, now - week, now);
    const previous = this.countMetricInRange(filter, now - week * 2, now - week);
    return current >= previous ? 'up' : 'down';
  }

  priorityClass(item: ChamadoTI | null | undefined): string {
    const priority = this.normalize(item?.prioridade);
    if (priority.includes('critica')) {
      return 'critical';
    }
    if (priority.includes('alta')) {
      return 'high';
    }
    if (priority.includes('baixa')) {
      return 'low';
    }
    return 'medium';
  }

  priorityLabel(item: ChamadoTI | null | undefined): string {
    const priority = this.normalize(item?.prioridade);
    if (priority.includes('critica')) {
      return 'Critica';
    }
    if (priority.includes('alta')) {
      return 'Alta';
    }
    if (priority.includes('baixa')) {
      return 'Baixa';
    }
    return 'Media';
  }

  slaPercent(item: ChamadoTI): number {
    const limit = this.getSlaLimitHours(item);
    if (limit <= 0) {
      return 0;
    }

    return Math.min(100, Math.round((this.getSlaElapsedHours(item) / limit) * 100));
  }

  slaClass(item: ChamadoTI): string {
    if (this.isSlaExpired(item)) {
      return 'expired';
    }
    if (this.isSlaMet(item)) {
      return 'met';
    }
    return this.slaPercent(item) >= 80 ? 'warning' : 'ok';
  }

  slaRemainingText(item: ChamadoTI): string {
    if (this.isConcluido(item)) {
      return this.isSlaMet(item) ? 'SLA cumprido' : 'SLA vencido';
    }

    const remaining = this.getSlaLimitHours(item) - this.getSlaElapsedHours(item);
    if (remaining <= 0) {
      return `Vencido ha ${this.formatDuration(Math.abs(remaining))}`;
    }

    return `SLA restante: ${this.formatDuration(remaining)}`;
  }

  openedAgo(item: ChamadoTI): string {
    return `Aberto ha ${this.formatDuration(this.getSlaElapsedHours(item))}`;
  }

  ticketTimeline(item: ChamadoTI | null): TicketTimelineEvent[] {
    if (!item) {
      return [];
    }

    const events: TicketTimelineEvent[] = [
      { time: this.formatShortTime(item.dataAbertura), label: 'Chamado aberto' },
    ];

    if (item.responsavel?.trim()) {
      events.push({ time: this.formatShortTime(item.ultimaMovimentacao || item.dataAbertura), label: 'Tecnico assumiu' });
    }

    if (item.observacoesEncerramento?.trim()) {
      events.push({ time: this.formatShortTime(item.dataEncerramento || item.ultimaMovimentacao), label: 'Solucao registrada' });
    }

    if (item.dataReabertura) {
      events.push({ time: this.formatShortTime(item.dataReabertura), label: 'Chamado reaberto' });
    }

    if (item.dataEncerramento) {
      events.push({ time: this.formatShortTime(item.dataEncerramento), label: 'Chamado encerrado' });
    }

    if (item.satisfacaoNota) {
      events.push({ time: this.formatShortTime(item.dataAvaliacao || item.dataEncerramento), label: 'Usuario avaliou o atendimento' });
    }

    return events;
  }

  readonly responsavelOptions = computed(() => {
    const names = this.chamados()
      .map((item) => item.responsavel?.trim())
      .filter((name): name is string => !!name);
    return Array.from(new Set(names)).sort((a, b) => a.localeCompare(b, 'pt-BR', { sensitivity: 'base' }));
  });

  completedSearchHint(): string {
    if (!this.canManage() || this.activeTab() !== 'concluidos') {
      return '';
    }

    const hasSearch = !!this.normalize(this.filterTerm()) || !!this.dateFrom() || !!this.dateTo();
    if (hasSearch) {
      return `${this.filteredChamados().length} chamado(s) concluido(s) encontrado(s) pela consulta.`;
    }

    const hidden = Math.max(this.concluidos() - 10, 0);
    return hidden > 0
      ? `Mostrando os 10 concluídos mais recentes. Consulte por número, título ou data para exibir os demais.`
      : 'Mostrando todos os chamados concluidos.';
  }

  focusPendingEvaluation(approvalTemplate: TemplateRef<void>, satisfactionTemplate: TemplateRef<void>): void {
    this.filterTerm.set('');
    const pendingApproval = this.chamados().find((item) => this.canApprove(item));
    if (pendingApproval && approvalTemplate) {
      this.openApprovalModal(pendingApproval, approvalTemplate);
      return;
    }

    const pendingEvaluation = this.chamados().find((item) => this.canEvaluate(item));
    if (pendingEvaluation && satisfactionTemplate) {
      this.openSatisfactionModal(pendingEvaluation, satisfactionTemplate);
    }
  }

  openMovementAlert(alert: TicketMovementAlert): void {
    const item = this.chamados().find((chamado) => chamado.id === alert.ticketId);
    if (!item) {
      this.toastr.info(`Chamado #${alert.ticketId} não está mais na lista atual.`, 'TI');
      return;
    }

    this.activeTab.set('meus');
    this.select(item);
  }

  onAttachmentSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    this.clearSelectedFilePreview();

    if (file && !this.isAllowedAttachment(file)) {
      input.value = '';
      this.selectedAttachment = null;
      this.selectedFileName.set('');
      this.toastr.warning('Envie apenas PDF, DOC, DOCX ou imagens JPG, PNG, GIF e WEBP.', 'Formato invalido');
      return;
    }

    this.selectedAttachment = file;
    this.selectedFileName.set(file?.name ?? '');

    if (file) {
      this.previewSelectedAttachment(file);
    }
  }

  openCreateTicketModal(template: TemplateRef<void>): void {
    this.createTicketModalRef = this.modalService.show(template, {
      animated: true,
      backdrop: true,
      class: 'modal-lg modal-dialog-centered uniflow-modal-shell',
      ignoreBackdropClick: this.saving(),
      keyboard: !this.saving(),
    });
  }

  closeCreateTicketModal(): void {
    if (this.saving()) {
      return;
    }

    this.createTicketModalRef?.hide();
    this.createTicketModalRef = undefined;
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      if (this.form.invalid) {
        this.toastr.warning('Confira os campos obrigatórios antes de abrir o chamado.', 'Atenção');
      }
      return;
    }

    this.saving.set(true);
    const payload: ChamadoTIPayload = {
      ...this.form.getRawValue(),
      userid: this.user()?.id ?? 0,
    };

    this.service.create(payload, this.selectedAttachment).subscribe({
      next: (created) => {
        this.chamados.set([created, ...this.chamados()]);
        this.form.patchValue({
          titulo: '',
          descricao: '',
          unidade: this.getUserUnidadeName(),
          prioridade: 'Media',
          status: 'Aberto',
          responsavel: '',
          acessoRemotoUrl: '',
          acessoRemotoSenha: '',
          equipamentoNome: this.getBrowserComputerName(),
          equipamentoIp: '',
          equipamentoSistemaOperacional: this.getOperatingSystem(),
          observacoes: '',
        });
        this.selectedAttachment = null;
        this.selectedFileName.set('');
        this.clearSelectedFilePreview();
        if (this.anexoInput?.nativeElement) {
          this.anexoInput.nativeElement.value = '';
        }
        this.saving.set(false);
        this.closeCreateTicketModal();
        this.selected.set(created);
        this.createdTicketNumber.set(created.id);
        this.toastr.success(`Chamado #${created.id} aberto com sucesso.`, 'TI');
      },
      error: (error) => {
        this.saving.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível abrir o chamado.', error), 'Erro');
      },
    });
  }

  select(item: ChamadoTI): void {
    this.selected.set(item);
    this.detailTab.set('detalhes');
    this.clearAttachmentPreview();
    this.loadComunicacoes(item.id);
    if (this.ticketModalRef) {
      void this.connectChat(item.id);
    }
    this.adminForm.patchValue({
      titulo: item.titulo,
      categoria: item.categoria,
      descricao: item.descricao,
      solicitante: item.solicitante,
      unidade: item.unidade,
      departamento: item.departamento,
      prioridade: item.prioridade,
      status: item.status,
      responsavel: item.responsavel,
      acessoRemotoUrl: item.acessoRemotoUrl,
      acessoRemotoSenha: item.acessoRemotoSenha,
      equipamentoNome: item.equipamentoNome,
      equipamentoIp: item.equipamentoIp,
      equipamentoSistemaOperacional: item.equipamentoSistemaOperacional,
      observacoes: item.observacoes,
    });
    this.closeForm.reset({ observacoesEncerramento: item.observacoesEncerramento || '' });
    this.satisfactionForm.reset({ nota: item.satisfacaoNota ?? 0, comentario: '' });
  }

  openTicketModal(item: ChamadoTI, template: TemplateRef<void>, satisfactionTemplate?: TemplateRef<void>, approvalTemplate?: TemplateRef<void>): void {
    if (this.canApprove(item) && approvalTemplate) {
      this.openApprovalModal(item, approvalTemplate);
      return;
    }

    if (this.canEvaluate(item) && satisfactionTemplate) {
      this.openSatisfactionModal(item, satisfactionTemplate);
      return;
    }

    this.select(item);
    this.startCommunicationRefresh();
    this.ticketModalRef = this.modalService.show(template, {
      animated: true,
      backdrop: true,
      class: 'modal-xl modal-dialog-centered uniflow-modal-shell ti-ticket-modal-shell',
      ignoreBackdropClick: this.updating() || this.sendingMessage() || this.closing() || this.reopening() || this.rating(),
      keyboard: !(this.updating() || this.sendingMessage() || this.closing() || this.reopening() || this.rating()),
    });
    void this.connectChat(item.id);
  }

  closeTicketModal(): void {
    if (this.updating() || this.sendingMessage() || this.closing() || this.reopening() || this.rating()) {
      return;
    }

    this.clearAttachmentPreview();
    this.stopCommunicationRefresh();
    void this.disconnectChat();
    this.ticketModalRef?.hide();
    this.ticketModalRef = undefined;
  }

  openSatisfactionModal(item: ChamadoTI, template: TemplateRef<void>): void {
    this.select(item);
    this.stopCommunicationRefresh();
    this.ticketModalRef?.hide();
    this.ticketModalRef = undefined;
    this.satisfactionModalRef?.hide();
    this.satisfactionModalRef = this.modalService.show(template, {
      animated: true,
      backdrop: 'static',
      class: 'modal-lg modal-dialog-centered uniflow-modal-shell ti-satisfaction-modal-shell',
      ignoreBackdropClick: true,
      keyboard: false,
    });
  }

  update(): void {
    const selected = this.selected();
    if (!selected || this.adminForm.invalid || this.updating()) {
      this.adminForm.markAllAsTouched();
      if (this.adminForm.invalid) {
        this.toastr.warning('Confira os campos obrigatórios antes de salvar.', 'Atenção');
      }
      return;
    }

    this.updating.set(true);
    this.service.update(selected.id, this.adminForm.getRawValue()).subscribe({
      next: (updated) => {
        this.chamados.set(this.chamados().map((item) => item.id === updated.id ? updated : item));
        this.rememberTicketMovement(updated);
        this.selected.set(updated);
        this.updating.set(false);
        this.toastr.success('Chamado atualizado com sucesso.', 'TI');
      },
      error: (error) => {
        this.updating.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível atualizar o chamado.', error), 'Erro');
      },
    });
  }

  reopenTicket(): void {
    const selected = this.selected();
    if (!selected || this.reopening()) {
      return;
    }

    this.reopening.set(true);
    this.service.reopen(selected.id).subscribe({
      next: (updated) => {
        this.chamados.set(this.chamados().map((item) => item.id === updated.id ? updated : item));
        this.rememberTicketMovement(updated);
        this.selected.set(updated);
        this.adminForm.patchValue({ status: updated.status });
        this.closeForm.reset({ observacoesEncerramento: '' });
        this.reopening.set(false);
        this.toastr.success(`Chamado #${updated.id} reaberto.`, 'TI');
      },
      error: (error) => {
        this.reopening.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível reabrir o chamado.', error), 'Erro');
      },
    });
  }

  sendMessage(): void {
    const selected = this.selected();
    const mensagem = this.messageForm.controls.mensagem.value.trim();
    if (!selected || this.messageForm.invalid || this.sendingMessage()) {
      this.messageForm.markAllAsTouched();
      if (this.messageForm.invalid) {
        this.toastr.warning('Escreva uma mensagem antes de enviar.', 'Atenção');
      }
      return;
    }

    if (!this.hasResponsible(selected)) {
      this.toastr.info('Defina um responsável pelo chamado antes de usar o chat.', 'Chat');
      return;
    }

    this.sendingMessage.set(true);
    if (this.chatConnection?.state === 'Connected' && this.chatChamadoId === selected.id) {
      this.chatConnection.invoke('EnviarMensagem', selected.id, mensagem).then(
        () => {
          this.messageForm.reset({ mensagem: '' });
          this.sendingMessage.set(false);
        },
        () => this.sendMessageByHttp(selected, mensagem),
      );
      return;
    }

    this.sendMessageByHttp(selected, mensagem);
  }

  private sendMessageByHttp(selected: ChamadoTI, mensagem: string): void {
    this.service.sendComunicação(selected.id, mensagem).subscribe({
      next: (message) => {
        this.addChatMessage(message);
        this.rememberTicketMovement({
          ...selected,
          ultimaMovimentacao: message.dataCriacao,
        });
        this.messageForm.reset({ mensagem: '' });
        this.sendingMessage.set(false);
        this.toastr.success('Mensagem enviada.', 'Comunicação');
      },
      error: (error) => {
        this.sendingMessage.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível enviar a mensagem.', error), 'Erro');
      },
    });
  }

  closeTicket(): void {
    const selected = this.selected();
    if (!selected || this.closing() || this.closeForm.invalid) {
      this.closeForm.markAllAsTouched();
      if (this.closeForm.invalid) {
        this.toastr.warning('Informe as observacoes de encerramento antes de concluir.', 'Atenção');
      }
      return;
    }

    this.closing.set(true);
    this.service.close(selected.id, this.closeForm.controls.observacoesEncerramento.value).subscribe({
      next: (updated) => {
        this.chamados.set(this.chamados().map((item) => item.id === updated.id ? updated : item));
        this.rememberTicketMovement(updated);
        this.selected.set(updated);
        this.adminForm.patchValue({ status: updated.status });
        this.closeForm.patchValue({ observacoesEncerramento: updated.observacoesEncerramento });
        this.closing.set(false);
        this.toastr.success('Chamado encerrado com data e hora atuais.', 'TI');
        this.suggestKnowledgeBaseCreation(updated);
      },
      error: (error) => {
        this.closing.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível encerrar o chamado.', error), 'Erro');
      },
    });
  }

  setRating(nota: number): void {
    this.satisfactionForm.patchValue({ nota });
  }

  submitSatisfaction(): void {
    const selected = this.selected();
    if (!selected || !this.canEvaluate(selected) || this.satisfactionForm.invalid || this.rating()) {
      this.satisfactionForm.markAllAsTouched();
      if (this.satisfactionForm.invalid) {
        this.toastr.warning('Escolha uma nota de 1 a 5 estrelas.', 'Atenção');
      }
      return;
    }

    this.rating.set(true);
    const formValue = this.satisfactionForm.getRawValue();
    this.service.rateSatisfaction(selected.id, formValue.nota, formValue.comentario).subscribe({
      next: (updated) => {
        this.chamados.set(this.chamados().map((item) => item.id === updated.id ? updated : item));
        this.rememberTicketMovement(updated);
        this.selected.set(updated);
        this.satisfactionForm.reset({ nota: 0, comentario: '' });
        this.rating.set(false);
        this.satisfactionModalRef?.hide();
        this.satisfactionModalRef = undefined;
        this.toastr.success('Obrigado pela avaliação do atendimento.', 'Satisfação');
      },
      error: (error) => {
        this.rating.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível registrar a avaliação.', error), 'Erro');
      },
    });
  }

  satisfactionReportCount(): number {
    return this.getSatisfactionReportItems().length;
  }

  satisfactionReportAverage(): string {
    const items = this.getSatisfactionReportItems();
    if (items.length === 0) {
      return '0,0';
    }

    const average = items.reduce((sum, item) => sum + (item.satisfacaoNota ?? 0), 0) / items.length;
    return average.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  }

  generateSatisfactionPdf(): void {
    if (!this.canManage() || this.generatingReport()) {
      return;
    }

    const items = this.getSatisfactionReportItems();
    if (items.length === 0) {
      this.toastr.info('Nenhuma avaliação encontrada no período informado.', 'Relatório');
      return;
    }

    this.generatingReport.set(true);
    const reportWindow = window.open('', '_blank');
    if (!reportWindow) {
      this.generatingReport.set(false);
      this.toastr.error('Permita pop-ups para gerar o PDF do relatório.', 'Relatório');
      return;
    }

    reportWindow.document.write(this.buildSatisfactionReportHtml(items));
    reportWindow.document.close();
    reportWindow.focus();
    setTimeout(() => {
      reportWindow.print();
      this.generatingReport.set(false);
    }, 300);
  }

  previewAttachment(item = this.selected()): void {
    if (!item?.anexoImagemUrl) {
      this.toastr.info('Este chamado não possui anexo.', 'Anexo');
      return;
    }

    this.service.downloadAttachment(item.id).subscribe({
      next: (blob) => {
        this.clearAttachmentPreview();
        this.attachmentObjectUrl = URL.createObjectURL(blob);
        this.attachmentPreviewType.set(this.getAttachmentPreviewType(item.anexoImagemUrl, blob.type));
        this.attachmentPreviewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.attachmentObjectUrl));
      },
      error: () => this.toastr.error('Não foi possível carregar o anexo.', 'Erro'),
    });
  }

  downloadAttachment(item = this.selected()): void {
    if (!item?.anexoImagemUrl) {
      this.toastr.info('Este chamado não possui anexo.', 'Anexo');
      return;
    }

    this.service.downloadAttachment(item.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = this.attachmentFileName(item.anexoImagemUrl);
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toastr.error('Não foi possível baixar o anexo.', 'Erro'),
    });
  }

  downloadChatHistory(item = this.selected()): void {
    if (!item || !this.canManage()) {
      return;
    }

    this.service.downloadComunicacoes(item.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `chamado-${item.id}-histórico-chat.txt`;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toastr.error('Não foi possível baixar o histórico do chat.', 'Erro'),
    });
  }

  openRemoteAccess(item = this.selected()): void {
    if (!this.hasResponsible(item)) {
      this.toastr.info('Defina um responsável pelo chamado antes de iniciar o acesso remoto.', 'Acesso remoto');
      return;
    }

    const target = this.buildRealVncLink(item);
    if (!target) {
      this.toastr.info('Este chamado ainda não possui IP capturado para acesso remoto.', 'Acesso remoto');
      return;
    }

    this.copyRemoteAccessPassword(item);

    let launched = false;
    const markLaunched = (): void => {
      launched = true;
      window.removeEventListener('blur', markLaunched);
      document.removeEventListener('visibilitychange', markHidden);
    };
    const markHidden = (): void => {
      if (document.hidden) {
        markLaunched();
      }
    };

    window.addEventListener('blur', markLaunched, { once: true });
    document.addEventListener('visibilitychange', markHidden);
    window.location.href = target;

    window.setTimeout(() => {
      window.removeEventListener('blur', markLaunched);
      document.removeEventListener('visibilitychange', markHidden);
      if (!launched) {
        this.toastr.warning('RealVNC Viewer não parece estar instalado ou o protocolo de abertura não está configurado neste computador.', 'Acesso remoto');
      }
    }, 1600);
  }

  copyRemoteAccessPassword(item = this.selected()): void {
    const password = item?.acessoRemotoSenha?.trim() || this.adminForm.controls.acessoRemotoSenha.value.trim();
    if (!password || !navigator.clipboard) {
      return;
    }

    void navigator.clipboard.writeText(password).then(
      () => this.toastr.success('Senha de acesso remoto copiada para a area de transferencia.', 'Acesso remoto'),
      () => undefined,
    );
  }

  openRemoteChat(): void {
    const selected = this.selected();
    if (!selected) {
      return;
    }

    if (!this.hasResponsible(selected)) {
      this.toastr.info('Defina um responsável pelo chamado antes de usar o chat.', 'Chat');
      return;
    }

    this.detailTab.set('comunicacao');
    void this.connectChat(selected.id);
  }

  private buildRealVncLink(item: ChamadoTI | null = this.selected()): string {
    const ip = this.normalizeRemoteHost(item?.equipamentoIp || this.adminForm.controls.equipamentoIp.value);
    return ip ? `com.realvnc.vncviewer.connect://${ip}` : '';
  }

  private normalizeRemoteHost(value: string | null | undefined): string {
    const host = (value ?? '').trim().split(',')[0].trim();
    if (!host || host === '::1' || host === '127.0.0.1') {
      return '';
    }

    return host.startsWith('::ffff:') ? host.slice('::ffff:'.length) : host;
  }

  equipmentSummary(item = this.selected()): string {
    if (!item) {
      return 'Equipamento não identificado';
    }

    const name = item.equipamentoNome || 'Nome não informado';
    const ip = item.equipamentoIp || 'IP não identificado';
    const os = item.equipamentoSistemaOperacional || 'Sistema operacional não identificado';
    return `${name} - ${ip} - ${os}`;
  }

  attachmentFileName(path: string): string {
    return path.split('/').pop() || 'anexo';
  }

  private isMine(item: ChamadoTI): boolean {
    const current = this.user();
    if (!current) {
      return false;
    }

    return item.responsavel === current.nome || item.responsavel === current.email;
  }

  private isConcluido(item: ChamadoTI): boolean {
    return !!item.dataEncerramento || this.normalize(item.status) === 'concluido';
  }

  canEvaluate(item: ChamadoTI | null): boolean {
    const current = this.user();
    return (
      !!item?.dataEncerramento &&
      !!current &&
      item.userid === current.id &&
      !this.canManage() &&
      item.avaliacaoPendente &&
      !item.aprovacaoPendente
    );
  }

  canReopen(item: ChamadoTI | null): boolean {
    const current = this.user();
    return !!item && !!current && !!item.dataEncerramento && (this.canManage() || item.userid === current.id);
  }

  hasResponsible(item: ChamadoTI | null = this.selected()): boolean {
    return !!item?.responsavel?.trim();
  }

  private canApprove(item: ChamadoTI | null): boolean {
    const current = this.user();
    return !!item?.dataEncerramento && !!current && item.userid === current.id && !this.canManage() && item.aprovacaoPendente;
  }

  openApprovalModal(item: ChamadoTI, template: TemplateRef<void>): void {
    this.select(item);
    this.stopCommunicationRefresh();
    this.ticketModalRef?.hide();
    this.ticketModalRef = undefined;
    this.approvalModalRef?.hide();
    this.approvalModalRef = this.modalService.show(template, {
      animated: true,
      backdrop: 'static',
      class: 'modal-lg modal-dialog-centered uniflow-modal-shell ti-approval-modal-shell',
      ignoreBackdropClick: true,
      keyboard: false,
    });
  }

  closeApprovalModal(): void {
    if (this.approving()) {
      return;
    }
    this.approvalModalRef?.hide();
    this.approvalModalRef = undefined;
  }

  submitApproval(): void {
    const selected = this.selected();
    if (!selected || !this.canApprove(selected) || this.approving()) {
      return;
    }

    this.approving.set(true);
    const observacoes = this.approvalForm.controls.observacoesAprovacao.value.trim();
    this.service.approve(selected.id, observacoes).subscribe({
      next: (updated) => {
        this.chamados.set(this.chamados().map((item) => item.id === updated.id ? updated : item));
        this.selected.set(updated);
        this.approvalForm.reset({ observacoesAprovacao: '' });
        this.approving.set(false);
        this.approvalModalRef?.hide();
        this.approvalModalRef = undefined;
        this.toastr.success('Chamado aprovado com sucesso. Agora é hora da pesquisa de satisfação.', 'Aprovação');
        if (this.satisfactionModalTemplate) {
          this.openSatisfactionModal(updated, this.satisfactionModalTemplate);
        }
      },
      error: (error) => {
        this.approving.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível aprovar o chamado.', error), 'Erro');
      },
    });
  }

  private loadComunicacoes(id: number): void {
    this.loadingComunicacoes.set(true);
    this.service.listComunicacoes(id).subscribe({
      next: (items) => {
        this.comunicacoes.set(items);
        this.markUnreadMessagesAsRead(items);
        this.loadingComunicacoes.set(false);
      },
      error: (error) => {
        this.comunicacoes.set([]);
        this.loadingComunicacoes.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível carregar a comunicação.', error), 'Erro');
      },
    });
  }

  private openRequestedChat(items: ChamadoTI[]): void {
    const params = this.route.snapshot.queryParamMap;
    if (params.get('chat') !== '1' || !this.ticketModalTemplate) {
      return;
    }

    const chamadoId = Number(params.get('chamadoId'));
    const item = items.find((chamado) => chamado.id === chamadoId);
    if (!item) {
      return;
    }

    this.openTicketModal(item, this.ticketModalTemplate);
    this.detailTab.set('comunicacao');
    void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
  }

  private openQrRequestedTicket(): void {
    const params = this.route.snapshot.queryParamMap;
    if (params.get('abrirChamado') !== '1' || !this.createTicketModalTemplate) {
      return;
    }

    this.form.patchValue({
      titulo: params.get('titulo') || this.form.controls.titulo.value,
      categoria: params.get('categoria') || 'Equipamento',
      solicitante: params.get('solicitante') || this.form.controls.solicitante.value,
      unidade: params.get('unidade') || this.form.controls.unidade.value,
      departamento: params.get('departamento') || this.form.controls.departamento.value,
      prioridade: params.get('prioridade') || 'Media',
      equipamentoNome: params.get('equipamentoNome') || this.form.controls.equipamentoNome.value,
      equipamentoSistemaOperacional: params.get('equipamentoSistemaOperacional') || this.form.controls.equipamentoSistemaOperacional.value,
      observacoes: params.get('observacoes') || this.form.controls.observacoes.value,
    });

    const descricao = this.form.controls.descricao.value.trim();
    if (!descricao) {
      this.form.patchValue({ descricao: 'Chamado aberto via QR Code do inventario de equipamentos.' });
    }

    this.openCreateTicketModal(this.createTicketModalTemplate);
    void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
  }

  private async connectChat(chamadoId: number): Promise<void> {
    if (!this.isBrowser) {
      return;
    }

    if (this.chatConnection?.state === 'Connected' && this.chatChamadoId === chamadoId) {
      return;
    }

    await this.disconnectChat();

    const signalR = await import('@microsoft/signalr');
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/chamados-ti-chat', {
        accessTokenFactory: () => this.auth.token() ?? '',
      })
      .withAutomaticReconnect()
      .build();

    connection.on('MensagemRecebida', (message: ChamadoTIComunicação) => {
      if (this.selected()?.id !== message.chamadoTIId) {
        return;
      }

      this.addChatMessage(message);
      this.markMessageAsRead(message);
      const selected = this.selected();
      if (selected) {
        this.rememberTicketMovement({ ...selected, ultimaMovimentacao: message.dataCriacao });
      }
    });

    connection.on('MensagemLida', (message: ChamadoTIComunicação) => this.updateChatMessage(message));

    connection.onreconnected(() => {
      if (this.chatChamadoId) {
        void connection.invoke('EntrarNoChamado', this.chatChamadoId);
      }
    });

    try {
      await connection.start();
      await connection.invoke('EntrarNoChamado', chamadoId);
      this.chatConnection = connection;
      this.chatChamadoId = chamadoId;
      this.stopCommunicationRefresh();
    } catch {
      this.chatConnection = null;
      this.chatChamadoId = null;
      this.startCommunicationRefresh();
    }
  }

  private async disconnectChat(): Promise<void> {
    const connection = this.chatConnection;
    const chamadoId = this.chatChamadoId;
    this.chatConnection = null;
    this.chatChamadoId = null;

    if (!connection) {
      return;
    }

    try {
      if (connection.state === 'Connected' && chamadoId) {
        await connection.invoke('SairDoChamado', chamadoId);
      }
      await connection.stop();
    } catch {
      await connection.stop().catch(() => undefined);
    }
  }

  private addChatMessage(message: ChamadoTIComunicação): void {
    if (this.comunicacoes().some((item) => item.id === message.id)) {
      return;
    }

    this.comunicacoes.set([...this.comunicacoes(), message].sort((a, b) =>
      new Date(a.dataCriacao).getTime() - new Date(b.dataCriacao).getTime(),
    ));
  }

  private updateChatMessage(message: ChamadoTIComunicação): void {
    this.comunicacoes.set(this.comunicacoes().map((item) => item.id === message.id ? message : item));
  }

  private markUnreadMessagesAsRead(messages: ChamadoTIComunicação[]): void {
    messages.forEach((message) => this.markMessageAsRead(message));
  }

  private markMessageAsRead(message: ChamadoTIComunicação): void {
    const currentUserId = this.user()?.id;
    if (!currentUserId || message.autorUserId === currentUserId || message.dataLeitura) {
      return;
    }

    this.service.markComunicaçãoRead(message.chamadoTIId, message.id).subscribe({
      next: (updated) => this.updateChatMessage(updated),
      error: () => undefined,
    });
  }

  private startCommunicationRefresh(): void {
    this.stopCommunicationRefresh();
    if (!this.isBrowser) {
      return;
    }

    this.communicationRefreshId = setInterval(() => this.refreshComunicacoesSilently(), 3000);
  }

  private stopCommunicationRefresh(): void {
    if (!this.communicationRefreshId) {
      return;
    }

    clearInterval(this.communicationRefreshId);
    this.communicationRefreshId = null;
  }

  private refreshComunicacoesSilently(): void {
    const selected = this.selected();
    if (!selected || !this.ticketModalRef) {
      return;
    }

    this.service.listComunicacoes(selected.id).subscribe({
      next: (items) => {
        if (this.selected()?.id === selected.id && this.ticketModalRef) {
          this.comunicacoes.set(items);
        }
      },
      error: () => undefined,
    });
  }

  private startTicketMovementRefresh(): void {
    this.stopTicketMovementRefresh();
    if (!this.isBrowser) {
      return;
    }

    this.ticketMovementRefreshId = setInterval(() => this.refreshTicketMovements(), 10000);
  }

  private stopTicketMovementRefresh(): void {
    if (!this.ticketMovementRefreshId) {
      return;
    }

    clearInterval(this.ticketMovementRefreshId);
    this.ticketMovementRefreshId = null;
  }

  private refreshTicketMovements(): void {
    const currentUser = this.user();
    if (!currentUser || !this.canManage()) {
      return;
    }

    this.service.list().subscribe({
      next: (items) => {
        const currentIds = new Set(items.map((item) => item.id));
        for (const id of [...this.knownTicketMovements.keys()]) {
          if (!currentIds.has(id)) {
            this.knownTicketMovements.delete(id);
          }
        }

        const movedTickets = items.filter((item) => this.detectAssignedTicketMovement(item));
        this.chamados.set(items);

        for (const item of movedTickets) {
          this.addMovementAlert(item);
        }
      },
      error: () => undefined,
    });
  }

  private detectAssignedTicketMovement(item: ChamadoTI): boolean {
    const key = item.ultimaMovimentacao || item.dataAbertura;
    const previous = this.knownTicketMovements.get(item.id);
    this.knownTicketMovements.set(item.id, key);

    if (!previous || previous === key || !this.isMine(item) || this.isConcluido(item)) {
      return false;
    }

    return true;
  }

  private addMovementAlert(item: ChamadoTI): void {
    this.toastr.info(`Chamado #${item.id} respondido.`, 'TI');

    const alert: TicketMovementAlert = {
      id: ++this.movementAlertSequence,
      ticketId: item.id,
      title: item.titulo,
      date: item.ultimaMovimentacao || new Date().toISOString(),
    };

    this.movementAlerts.set([alert, ...this.movementAlerts().filter((existing) => existing.ticketId !== item.id)].slice(0, 4));
  }

  private rememberTicketMovement(item: Pick<ChamadoTI, 'id' | 'ultimaMovimentacao' | 'dataAbertura'>): void {
    this.knownTicketMovements.set(item.id, item.ultimaMovimentacao || item.dataAbertura);
  }

  private matchesFilter(item: ChamadoTI, term: string): boolean {
    const createdAt = new Date(item.dataAbertura);
    const closedAt = item.dataEncerramento ? new Date(item.dataEncerramento) : null;
    const formattedDate = Number.isNaN(createdAt.getTime()) ? '' : createdAt.toLocaleDateString('pt-BR');
    const formattedClosedDate = closedAt && !Number.isNaN(closedAt.getTime()) ? closedAt.toLocaleDateString('pt-BR') : '';
    const fields = [
      String(item.id),
      item.titulo,
      item.descricao,
      item.responsavel,
      item.dataAbertura,
      item.dataEncerramento,
      formattedDate,
      formattedClosedDate,
    ];

    return fields.some((value) => this.normalize(value).includes(term));
  }

  private getSatisfactionReportItems(): ChamadoTI[] {
    const from = this.parseDateFilter(this.reportDateFrom(), false);
    const to = this.parseDateFilter(this.reportDateTo(), true);

    return this.chamados()
      .filter((item) => !!item.satisfacaoNota && !!item.dataAvaliacao)
      .filter((item) => {
        const date = new Date(item.dataAvaliacao || '');
        if (Number.isNaN(date.getTime())) {
          return false;
        }

        return (!from || date >= from) && (!to || date <= to);
      })
      .sort((a, b) => new Date(b.dataAvaliacao || '').getTime() - new Date(a.dataAvaliacao || '').getTime());
  }

  private buildSatisfactionReportHtml(items: ChamadoTI[]): string {
    const now = new Date();
    const period = `${this.formatReportDate(this.reportDateFrom())} a ${this.formatReportDate(this.reportDateTo())}`;
    const average = this.satisfactionReportAverage();
    const excellent = items.filter((item) => (item.satisfacaoNota ?? 0) >= 4).length;
    const critical = items.filter((item) => (item.satisfacaoNota ?? 0) <= 2).length;
    const rows = items.map((item) => `
      <tr>
        <td>#${item.id}</td>
        <td>
          <strong>${this.escapeHtml(item.titulo)}</strong>
          <small>${this.escapeHtml(item.solicitante)} - ${this.escapeHtml(item.unidade)}</small>
        </td>
        <td>${this.escapeHtml(item.responsavel || 'Sem responsável')}</td>
        <td class="center">${item.satisfacaoNota}</td>
        <td>${this.formatDateTime(item.dataAvaliacao)}</td>
        <td>${this.escapeHtml(item.satisfacaoComentario || '-')}</td>
      </tr>
    `).join('');

    return `<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8" />
  <title>Relatório de satisfação - Chamados de TI</title>
  <style>
    @page { size: A4; margin: 16mm; }
    * { box-sizing: border-box; }
    body { margin: 0; color: #172033; font-family: Arial, Helvetica, sans-serif; background: #ffffff; }
    header { display: flex; justify-content: space-between; gap: 24px; padding-bottom: 18px; border-bottom: 3px solid #1f5f8b; }
    .brand p { margin: 0 0 4px; color: #1f5f8b; font-size: 11px; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; }
    h1 { margin: 0; font-size: 24px; }
    .meta { color: #64748b; font-size: 12px; text-align: right; }
    .summary { display: grid; grid-template-columns: repeat(4, 1fr); gap: 10px; margin: 20px 0; }
    .summary article { padding: 12px; border: 1px solid #d8e0ea; border-radius: 8px; background: #f8fafc; }
    .summary span { display: block; color: #64748b; font-size: 11px; font-weight: 700; text-transform: uppercase; }
    .summary strong { display: block; margin-top: 6px; color: #1f5f8b; font-size: 22px; }
    table { width: 100%; border-collapse: collapse; font-size: 11px; }
    th { padding: 9px 8px; background: #172033; color: #ffffff; text-align: left; }
    td { padding: 9px 8px; border-bottom: 1px solid #e2e8f0; vertical-align: top; }
    td small { display: block; margin-top: 3px; color: #64748b; }
    tr:nth-child(even) td { background: #f8fafc; }
    .center { text-align: center; font-weight: 800; color: #b45309; }
    footer { margin-top: 18px; padding-top: 10px; border-top: 1px solid #d8e0ea; color: #64748b; font-size: 10px; }
  </style>
</head>
<body>
  <header>
    <div class="brand">
      <p>UniFlowHub</p>
      <h1>Relatório de satisfação dos chamados de TI</h1>
    </div>
    <div class="meta">
      <div>Periodo: ${period}</div>
      <div>Gerado em: ${this.formatDateTime(now.toISOString())}</div>
    </div>
  </header>
  <section class="summary">
    <article><span>Avaliacoes</span><strong>${items.length}</strong></article>
    <article><span>Nota media</span><strong>${average}</strong></article>
    <article><span>Notas 4 e 5</span><strong>${excellent}</strong></article>
    <article><span>Notas 1 e 2</span><strong>${critical}</strong></article>
  </section>
  <table>
    <thead>
      <tr>
        <th>Chamado</th>
        <th>Atendimento</th>
        <th>Responsavel</th>
        <th>Nota</th>
        <th>Avaliação</th>
        <th>Comentario</th>
      </tr>
    </thead>
    <tbody>${rows}</tbody>
  </table>
  <footer>Documento gerado pelo UniFlowHub para acompanhamento interno da qualidade dos atendimentos.</footer>
</body>
</html>`;
  }

  private buildAssistantGuidance(text: string): string {
    const normalized = this.normalize(text);
    if (normalized.includes('senha') || normalized.includes('acesso')) {
      return 'Sugestao inicial: confirme usuario, sistema afetado, mensagem de erro e se o acesso falha em outro navegador. Se for senha, tente redefinicao pelo fluxo padrao antes de abrir como incidente.';
    }

    if (normalized.includes('internet') || normalized.includes('rede') || normalized.includes('link') || normalized.includes('wifi')) {
      return 'Sugestao inicial: verifique se outros usuarios da unidade tambem foram afetados, teste cabo/Wi-Fi, reinicie a conexao e informe a unidade para checagem do link.';
    }

    if (normalized.includes('impressora') || normalized.includes('scanner')) {
      return 'Sugestao inicial: confirme se a impressora esta ligada, com papel/toner, se aparece offline e qual documento ficou preso na fila.';
    }

    if (normalized.includes('computador') || normalized.includes('notebook') || normalized.includes('lento')) {
      return 'Sugestao inicial: reinicie o equipamento, feche programas pesados e informe desde quando ocorre. Inclua prints ou anexo se houver mensagem de erro.';
    }

    return 'Ainda não encontrei um artigo igual. Descreva impacto, sistema afetado, horário de início, unidade e qualquer mensagem de erro para acelerar o atendimento.';
  }

  private buildAssistantLocalContext(text: string): string {
    const matches = this.findKnowledgeMatches(`${this.form.controls.titulo.value} ${text}`);
    if (matches.length === 0) {
      return 'Nenhum artigo local semelhante encontrado na base de conhecimento.';
    }

    return matches
      .map((item) => `Artigo #${item.id}: ${item.titulo}. Categoria: ${item.categoria}. Tags: ${item.tags}. Resumo: ${item.descricao}`)
      .join('\n');
  }

  private findKnowledgeMatches(text: string): BaseConhecimentoTI[] {
    const terms = this.normalize(text)
      .split(/\s+/)
      .filter((term) => term.length >= 4);

    if (terms.length === 0) {
      return [];
    }

    return this.conhecimentos()
      .map((item) => ({
        item,
        score: terms.filter((term) => this.normalize(`${item.titulo} ${item.categoria} ${item.descricao} ${item.tags}`).includes(term)).length,
      }))
      .filter(({ score }) => score > 0)
      .sort((a, b) => b.score - a.score)
      .map(({ item }) => item)
      .slice(0, 3);
  }

  private suggestKnowledgeBaseCreation(item: ChamadoTI): void {
    if (!this.canManage() || !item.observacoesEncerramento?.trim()) {
      return;
    }

    const duplicate = this.findDuplicateKnowledge(item);
    if (duplicate) {
      this.toastr.info(`A IA encontrou artigo parecido na base: ${duplicate.titulo}. Novo cadastro bloqueado.`, 'TI Assistant');
      return;
    }

    const confirmed = window.confirm(`TI Assistant: deseja incluir a solução do chamado #${item.id} na base de conhecimento?`);
    if (!confirmed) {
      return;
    }

    this.baseConhecimentoService.create({
      titulo: `Chamado #${item.id} - ${item.titulo}`,
      categoria: item.categoria || 'Chamados',
      descricao: item.observacoesEncerramento,
      tags: `chamado-${item.id}, ${item.departamento}, ${item.unidade}, ${item.categoria}`,
    }).subscribe({
      next: (created) => {
        this.conhecimentos.set([created, ...this.conhecimentos()]);
        this.toastr.success(`Base de conhecimento criada a partir do chamado #${item.id}.`, 'TI Assistant');
      },
      error: (error) => this.toastr.error(this.getErrorMessage('Não foi possível criar a base de conhecimento.', error), 'TI Assistant'),
    });
  }

  private findDuplicateKnowledge(item: ChamadoTI): BaseConhecimentoTI | null {
    const ticketTag = `chamado-${item.id}`;
    const normalizedTitle = this.normalize(item.titulo);
    const solutionTerms = this.normalize(item.observacoesEncerramento)
      .split(/\s+/)
      .filter((term) => term.length >= 5);

    return this.conhecimentos().find((article) => {
      const articleText = this.normalize(`${article.titulo} ${article.descricao} ${article.tags}`);
      const sameTicket = articleText.includes(ticketTag);
      const sameTitle = normalizedTitle.length >= 8 && articleText.includes(normalizedTitle);
      const sharedSolutionTerms = solutionTerms.filter((term) => articleText.includes(term)).length;
      return sameTicket || sameTitle || sharedSolutionTerms >= 5;
    }) ?? null;
  }

  private applyMetricFilter(items: ChamadoTI[]): ChamadoTI[] {
    const filter = this.metricFilter();
    if (filter === 'abertos') {
      return items.filter((item) => this.normalize(item.status).includes('aberto'));
    }

    if (filter === 'sla-cumprido') {
      return items.filter((item) => this.isSlaMet(item));
    }

    if (filter === 'sla-vencido') {
      return items.filter((item) => this.isSlaExpired(item));
    }

    if (filter === 'em-atendimento') {
      return items.filter((item) => !this.isConcluido(item) && !!item.responsavel?.trim());
    }

    if (filter === 'respondidos') {
      const respondedIds = new Set(this.movementAlerts().map((alert) => alert.ticketId));
      return items.filter((item) => respondedIds.has(item.id));
    }

    return items;
  }

  private countMetricInRange(filter: MetricFilter, start: number, end: number): number {
    return this.chamados().filter((item) => {
      const itemDate = new Date(item.dataAbertura).getTime();
      return itemDate >= start && itemDate < end && this.applyMetricFilterToItem(item, filter);
    }).length;
  }

  private applyMetricFilterToItem(item: ChamadoTI, filter: MetricFilter): boolean {
    if (filter === 'abertos') {
      return this.normalize(item.status).includes('aberto');
    }
    if (filter === 'sla-cumprido') {
      return this.isSlaMet(item);
    }
    if (filter === 'sla-vencido') {
      return this.isSlaExpired(item);
    }
    if (filter === 'em-atendimento') {
      return !this.isConcluido(item) && !!item.responsavel?.trim();
    }
    if (filter === 'respondidos') {
      return this.movementAlerts().some((alert) => alert.ticketId === item.id);
    }
    return true;
  }

  private isAwaitingUser(item: ChamadoTI): boolean {
    const status = this.normalize(item.status);
    return status.includes('aguard') || status.includes('usuario') || status.includes('respondido');
  }

  private getSlaLimitHours(item: ChamadoTI): number {
    const priority = this.normalize(item.prioridade);
    if (priority.includes('critica')) {
      return 4;
    }

    if (priority.includes('alta')) {
      return 8;
    }

    if (priority.includes('baixa')) {
      return 72;
    }

    return 24;
  }

  private getSlaElapsedHours(item: ChamadoTI): number {
    const start = new Date(item.dataAbertura).getTime();
    const end = item.dataEncerramento ? new Date(item.dataEncerramento).getTime() : Date.now();
    if (Number.isNaN(start) || Number.isNaN(end)) {
      return 0;
    }

    return Math.max(0, (end - start) / 36e5);
  }

  private isSlaMet(item: ChamadoTI): boolean {
    return this.isConcluido(item) && this.getSlaElapsedHours(item) <= this.getSlaLimitHours(item);
  }

  private isSlaExpired(item: ChamadoTI): boolean {
    return !this.isSlaMet(item) && this.getSlaElapsedHours(item) > this.getSlaLimitHours(item);
  }

  private formatDuration(hours: number): string {
    const totalMinutes = Math.max(0, Math.round(hours * 60));
    const days = Math.floor(totalMinutes / 1440);
    const remainingAfterDays = totalMinutes % 1440;
    const fullHours = Math.floor(remainingAfterDays / 60);
    const minutes = remainingAfterDays % 60;

    if (days > 0) {
      return `${days}d ${fullHours}h`;
    }

    if (fullHours > 0) {
      return `${fullHours}h ${minutes}min`;
    }

    return `${minutes}min`;
  }

  private formatShortTime(value: string | null | undefined): string {
    if (!value) {
      return '--:--';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '--:--';
    }

    return date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
  }

  private getFilterDate(item: ChamadoTI, useClosingDate: boolean): Date {
    const date = new Date(useClosingDate ? item.dataEncerramento || item.dataAbertura : item.dataAbertura);
    return Number.isNaN(date.getTime()) ? new Date(0) : date;
  }

  private sortConcluidos(items: ChamadoTI[]): ChamadoTI[] {
    return [...items].sort((a, b) => {
      const dateA = this.getFilterDate(a, true).getTime();
      const dateB = this.getFilterDate(b, true).getTime();
      return dateB - dateA || b.id - a.id;
    });
  }

  private normalize(value: string | number | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  private safePage(): number {
    return Math.min(Math.max(this.page(), 1), this.totalPages());
  }

  private sortItems(items: ChamadoTI[]): ChamadoTI[] {
    const field = this.sortField();
    const direction = this.sortDirection() === 'asc' ? 1 : -1;
    return items.slice().sort((a, b) => {
      const aValue = a[field];
      const bValue = b[field];
      const result = typeof aValue === 'number' && typeof bValue === 'number'
        ? aValue - bValue
        : this.normalize(aValue as string | number | null | undefined).localeCompare(this.normalize(bValue as string | number | null | undefined));
      return result * direction;
    });
  }

  private getUserUnidadeName(): string {
    const user = this.user();
    if (!user) {
      return '';
    }

    return user.unidadeNome || this.unidades().find((unidade) => unidade.id === user.unidadeId)?.nome || '';
  }

  private getBrowserComputerName(): string {
    return 'Não informado pelo navegador';
  }

  private getOperatingSystem(): string {
    if (!this.isBrowser) {
      return '';
    }

    const userAgent = navigator.userAgent;
    const navigatorWithData = navigator as Navigator & { userAgentData?: { platform?: string } };
    const platform = navigatorWithData.userAgentData?.platform || navigator.platform || '';

    if (/windows/i.test(userAgent) || /win/i.test(platform)) {
      return 'Windows';
    }

    if (/android/i.test(userAgent)) {
      return 'Android';
    }

    if (/iphone|ipad|ipod/i.test(userAgent)) {
      return 'iOS';
    }

    if (/mac/i.test(platform)) {
      return 'macOS';
    }

    if (/linux/i.test(platform)) {
      return 'Linux';
    }

    return platform || 'Não identificado';
  }

  private parseDateFilter(value: string, endOfDay: boolean): Date | null {
    if (!value) {
      return null;
    }

    const date = new Date(`${value}T${endOfDay ? '23:59:59.999' : '00:00:00.000'}`);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  private formatReportDate(value: string): string {
    if (!value) {
      return 'todos os períodos';
    }

    return this.formatDateTime(`${value}T00:00:00`).slice(0, 10);
  }

  private formatDateTime(value: string | null | undefined): string {
    if (!value) {
      return '-';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '-';
    }

    return date.toLocaleString('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  logout(): void {
    this.auth.logout();
  }

  goHome(): void {
    void this.router.navigate(['/hub']);
  }

  openMonitoring(): void {
    void this.router.navigate(['/ti/monitoramento']);
  }

  editProfile(): void {
    this.profileMenuOpen.set(false);
    this.profileFlow.editProfile();
  }

  changePassword(): void {
    this.profileMenuOpen.set(false);
    this.profileFlow.changePassword();
  }

  @HostListener('document:click', ['$event'])
  closeProfileMenuOnDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('.profile-area')) {
      this.profileMenuOpen.set(false);
    }
  }

  private isAllowedAttachment(file: File): boolean {
    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    return ALLOWED_ATTACHMENT_TYPES.includes(file.type) || ALLOWED_ATTACHMENT_EXTENSIONS.includes(extension);
  }

  private previewSelectedAttachment(file: File): void {
    this.selectedAttachmentObjectUrl = URL.createObjectURL(file);
    this.selectedFilePreviewType.set(this.getAttachmentPreviewType(file.name, file.type));
    this.selectedFilePreviewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.selectedAttachmentObjectUrl));
  }

  private getAttachmentPreviewType(path: string, mimeType = ''): 'image' | 'pdf' | 'download' {
    const extension = path.slice(path.lastIndexOf('.')).toLowerCase();
    if (mimeType.startsWith('image/') || ['.jpg', '.jpeg', '.png', '.gif', '.webp'].includes(extension)) {
      return 'image';
    }

    if (mimeType === 'application/pdf' || extension === '.pdf') {
      return 'pdf';
    }

    return 'download';
  }

  private clearAttachmentPreview(): void {
    if (this.attachmentObjectUrl) {
      URL.revokeObjectURL(this.attachmentObjectUrl);
      this.attachmentObjectUrl = '';
    }

    this.attachmentPreviewUrl.set(null);
    this.attachmentPreviewType.set('');
  }

  private clearSelectedFilePreview(): void {
    if (this.selectedAttachmentObjectUrl) {
      URL.revokeObjectURL(this.selectedAttachmentObjectUrl);
      this.selectedAttachmentObjectUrl = '';
    }

    this.selectedFilePreviewUrl.set(null);
    this.selectedFilePreviewType.set('');
  }

  private getErrorMessage(fallback: string, error?: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error;
      }

      if (error.error?.title) {
        return error.error.title;
      }
    }

    return fallback;
  }
}

