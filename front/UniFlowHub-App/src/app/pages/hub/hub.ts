import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, HostListener, OnDestroy, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, firstValueFrom, forkJoin, of } from 'rxjs';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { AutoRefreshControlComponent } from '../../core/auto-refresh-control.component';
import { ChamadosTIService } from '../../core/chamados-ti.service';
import { ComprasService } from '../../core/compras.service';
import { EquipamentosTIService } from '../../core/equipamentos-ti.service';
import { ChamadoTI, EquipamentoTI, SolicitacaoCompra, SolicitacaoRH, User } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { SolicitacoesService } from '../../core/solicitacoes.service';
import { ThemeService } from '../../core/theme.service';

type DashboardArea = 'admin' | 'rh' | 'ti' | 'compras' | 'controladoria' | 'financeiro' | 'padrao';
const USEFUL_LINKS_KEY = 'uniflowhub.usefulLinks';
const MONITORING_STORAGE_KEY = 'uniflowhub.ti.monitoramento';
const TI_CHILD_ACCESSES = ['ti-admin', 'base-conhecimento-ti', 'equipamentos-ti'];

type MonitorStatus = 'online' | 'offline' | 'testando' | 'pendente';

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

interface MetricCard {
  label: string;
  value: number | string;
  detail: string;
  tone: 'neutral' | 'attention' | 'danger' | 'success';
  route?: string;
}

interface AlertItem {
  id: string;
  sector: string;
  title: string;
  detail: string;
  priority: string;
  status: string;
  route: string;
  date: string;
}

interface DashboardAction {
  label: string;
  route: string;
  description: string;
  enabled: boolean;
}

interface UsefulLink {
  label: string;
  url: string;
  description: string;
  icon: 'outlook' | 'whatsapp' | 'custom';
  locked: boolean;
}

interface BirthdayItem {
  id: number;
  nome: string;
  departamento: string;
  cargo: string;
  day: number;
  isToday: boolean;
}

interface ForecastDay {
  date: string;
  label: string;
  icon: string;
  iconTone: string;
  temperatureMin: number;
  temperatureMax: number;
  rainChance: number;
  summary: string;
}

@Component({
  selector: 'app-hub',
  imports: [AutoRefreshControlComponent],
  templateUrl: './hub.html',
  styleUrl: './hub.scss',
})
export class HubPage implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly http = inject(HttpClient);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly rhService = inject(SolicitacoesService);
  private readonly tiService = inject(ChamadosTIService);
  private readonly equipamentosService = inject(EquipamentosTIService);
  private readonly comprasService = inject(ComprasService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly profileMenuOpen = signal(false);
  readonly loading = signal(false);
  readonly atualizadoEm = signal<Date | null>(null);
  readonly rhItems = signal<SolicitacaoRH[]>([]);
  readonly tiItems = signal<ChamadoTI[]>([]);
  readonly equipamentosTi = signal<EquipamentoTI[]>([]);
  readonly monitoramentoTi = signal<MonitorItem[]>([]);
  readonly comprasItems = signal<SolicitacaoCompra[]>([]);
  readonly users = signal<User[]>([]);
  readonly forecast = signal<ForecastDay[]>([]);
  readonly forecastLocation = signal('Porto Alegre');
  readonly forecastLoading = signal(false);
  readonly forecastError = signal('');
  private reminderIntervalId: ReturnType<typeof setInterval> | null = null;
  readonly usefulLinks = signal<UsefulLink[]>([
    {
      label: 'Outlook',
      url: 'https://outlook.com',
      description: 'Email, agenda e contatos Microsoft.',
      icon: 'outlook',
      locked: true,
    },
    {
      label: 'WhatsApp Web',
      url: 'https://web.whatsapp.com',
      description: 'Mensagens e atendimento pelo navegador.',
      icon: 'whatsapp',
      locked: true,
    },
  ]);
  readonly newLinkName = signal('');
  readonly newLinkUrl = signal('');
  readonly newLinkDescription = signal('');

  readonly greeting = computed(() => {
    const firstName = (this.user()?.nome || 'Usuário').trim().split(/\s+/)[0];
    const hour = new Date().getHours();
    const period = hour < 12 ? 'Bom dia' : hour < 18 ? 'Boa tarde' : 'Boa noite';
    return `${period}, ${firstName}`;
  });

  readonly currentArea = computed<DashboardArea>(() => {
    const user = this.user();
    const role = user?.role;
    const department = this.normalize(user?.departamento);

    if (role === 'Admin') {
      return 'admin';
    }

    if (this.isRhUser()) {
      return 'rh';
    }

    if (this.auth.hasAnyAccess(TI_CHILD_ACCESSES) || role === 'TI' || this.isTiDepartment(department)) {
      return 'ti';
    }

    if (role === 'Compras' || role === 'Diretoria' || department.includes('compra')) {
      return 'compras';
    }

    if (role === 'Controladoria' || department.includes('controladoria')) {
      return 'controladoria';
    }

    if (department.includes('financeiro')) {
      return 'financeiro';
    }

    return 'padrao';
  });

  readonly dashboardTitle = computed(() => {
    const area = this.currentArea();
    const user = this.user();

    if (area === 'admin') {
      return 'Dashboard da Administração';
    }

    if (area === 'rh') {
      return this.auth.hasAnyRole(['RH']) ? 'Dashboard da Administração de RH' : 'Dashboard de Recursos Humanos';
    }

    if (area === 'ti') {
      return this.auth.hasAnyRole(['TI']) ? 'Dashboard da Administração de TI' : 'Dashboard de TI';
    }

    if (area === 'compras') {
      return this.auth.hasAnyRole(['Compras', 'Diretoria']) ? 'Dashboard da Administração de Compras' : 'Dashboard de Compras';
    }

    if (area === 'controladoria') {
      return 'Dashboard da Controladoria';
    }

    if (area === 'financeiro') {
      return 'Dashboard Financeiro';
    }

    return 'Dashboard Padrão';
  });

  readonly dashboardSubtitle = computed(() => {
    const user = this.user();
    if (this.currentArea() === 'admin') {
      return 'Acompanhe os alertas dos setores ativos e entre nas administrações quando precisar agir.';
    }

    if (this.currentArea() === 'padrao') {
      return `${user?.nome || 'Usuário'}, acompanhe seus chamados, solicitações e compras em aberto.`;
    }

    return `${user?.nome || 'Usuário'}, estes são os alertas priorizados para seu perfil e setor.`;
  });

  readonly dashboardScopeLabel = computed(() =>
    this.currentArea() === 'padrao' ? 'Dashboard padrão' : (this.user()?.departamento || 'Setor'),
  );

  readonly isBirthdayToday = computed(() => {
    const birthDate = this.user()?.dataNascimento;
    return !!birthDate && this.matchesToday(birthDate);
  });

  readonly birthdayGreeting = computed(() => {
    const firstName = (this.user()?.nome || 'você').trim().split(/\s+/)[0];
    return `Feliz aniversário, ${firstName}! Que seu dia seja leve, especial e cheio de boas notícias.`;
  });

  readonly showBirthdays = computed(() => this.currentArea() === 'rh' && this.auth.hasAccess('rh-admin'));

  readonly monthBirthdays = computed<BirthdayItem[]>(() => {
    const today = new Date();
    const currentMonth = today.getMonth();

    return this.users()
      .filter((user) => user.ativo)
      .map((user) => {
        const date = this.dateParts(user.dataNascimento);
        if (!date || date.month !== currentMonth) {
          return null;
        }

        return {
          id: user.id,
          nome: user.nome,
          departamento: user.departamento,
          cargo: user.cargo,
          day: date.day,
          isToday: date.day === today.getDate(),
        };
      })
      .filter((item): item is BirthdayItem => !!item)
      .sort((a, b) => a.day - b.day || a.nome.localeCompare(b.nome));
  });

  readonly metrics = computed<MetricCard[]>(() => {
    const area = this.currentArea();
    if (area === 'admin') {
      return [
        this.metric('Abertas agora', this.openRh().length + this.openTi().length + this.openCompras().length, 'RH, TI e Compras', 'attention'),
        this.metric('Prioridade alta', this.highPriorityAlerts().length, 'Solicitações críticas ou altas', 'danger'),
        this.metric('Reabertas', this.reopenedRh().length + this.reopenedTi().length, 'Itens que voltaram ao fluxo', 'danger'),
        this.metric('Administração', this.adminQueue().length, 'Pendências para responsáveis', 'neutral'),
      ];
    }

    if (area === 'rh') {
      const items = this.rhScope();
      return [
        this.metric('Abertas', items.filter((item) => this.isOpenStatus(item.status)).length, 'Solicitações em atendimento', 'attention'),
        this.metric('Reabertas', items.filter((item) => this.isReopened(item.status)).length, 'Retornaram para análise', 'danger'),
        this.metric('Alta prioridade', items.filter((item) => this.isHighPriority(item.prioridade)).length, 'Demandas sensíveis', 'danger'),
        this.metric('Sem responsável', items.filter((item) => !item.responsavel?.trim()).length, 'Precisam de triagem', 'neutral'),
      ];
    }

    if (area === 'ti') {
      const items = this.tiScope();
      const canViewMonitoring = this.auth.hasAnyAccess(TI_CHILD_ACCESSES);
      return [
        this.metric('Chamados abertos', items.filter((item) => this.isOpenStatus(item.status)).length, 'Chamados ativos no setor', 'attention'),
        ...(canViewMonitoring ? [this.metric('Chamados por setor', this.tiDepartmentCount(items), this.tiDepartmentSummary(items), 'neutral')] : []),
        this.metric('SLA cumprido', items.filter((item) => this.isTiSlaMet(item)).length, 'Fechados dentro do prazo', 'success'),
        this.metric('SLA vencido', items.filter((item) => this.isTiSlaExpired(item)).length, 'Fora do prazo por prioridade', 'danger'),
        ...(canViewMonitoring
          ? [
              this.metric('Equipamentos por unidade', this.equipamentosTi().length, this.equipmentUnitSummary(), 'neutral'),
              this.metric('Disponibilidade dos links', this.linkAvailability(items), this.linkAvailabilityDetail(), 'success', '/ti/monitoramento'),
            ]
          : []),
      ];
    }

    if (area === 'compras') {
      const items = this.comprasScope();
      return [
        this.metric('Aguardando diretoria', items.filter((item) => item.status === 'Aguardando Diretoria').length, 'Pendentes de aprovação', 'attention'),
        this.metric('Em compras', items.filter((item) => item.status.includes('Compras') || item.status === 'Em compras').length, 'Com comprador ou fila ativa', 'neutral'),
        this.metric('Alta prioridade', items.filter((item) => this.isHighPriority(item.prioridade)).length, 'Compras urgentes', 'danger'),
        this.metric('Concluídas', items.filter((item) => item.status === 'Concluida').length, 'Finalizadas no fluxo', 'success'),
      ];
    }

    if (area === 'controladoria') {
      return [
        this.metric('Guias de ICMS', 0, 'Acesse a tela para consultar o Oracle', 'neutral'),
        this.metric('Pendencias', 0, 'Indicadores serao exibidos no modulo', 'attention'),
        this.metric('Pagamentos', 0, 'Controle por status pago ou pendente', 'success'),
        this.metric('Integracao', 1, 'Banco Oracle configuravel', 'neutral'),
      ];
    }

    return this.defaultMetrics();
  });

  readonly alerts = computed<AlertItem[]>(() => {
    const area = this.currentArea();
    let alerts: AlertItem[] = [];

    if (area === 'admin') {
      alerts = [
        ...this.rhAlerts(this.rhItems()),
        ...this.tiAlerts(this.tiItems()),
        ...this.comprasAlerts(this.comprasItems()),
      ];
    } else if (area === 'rh') {
      alerts = this.rhAlerts(this.rhScope());
    } else if (area === 'ti') {
      alerts = [...this.tiAlerts(this.tiScope()), ...this.monitoramentoAlerts()];
    } else if (area === 'compras') {
      alerts = this.comprasAlerts(this.comprasScope());
    } else {
      alerts = this.defaultAlerts();
    }

    return alerts
      .sort((a, b) => this.priorityWeight(b.priority) - this.priorityWeight(a.priority) || this.dateTime(b.date) - this.dateTime(a.date))
      .slice(0, 8);
  });

  readonly actions = computed<DashboardAction[]>(() => {
    const area = this.currentArea();
    if (area === 'admin') {
      return [
        { label: 'Administrar RH', route: '/rh', description: 'Fila completa de solicitações de RH.', enabled: true },
        { label: 'Administrar TI', route: '/ti', description: 'Chamados, responsáveis e reaberturas.', enabled: true },
        { label: 'Administrar Compras', route: '/compras', description: 'Aprovações e etapa de compras.', enabled: true },
        { label: 'Controladoria', route: '/controladoria', description: 'Controle de guias de ICMS.', enabled: true },
        { label: 'Usuários', route: '/usuarios', description: 'Crie acessos e ajuste perfis.', enabled: this.auth.hasAnyRole(['Admin', 'TI']) },
      ];
    }

    if (area === 'rh') {
      return [
        { label: 'Abrir solicitação', route: '/solicitacoes', description: 'Registrar uma nova demanda de RH.', enabled: true },
        { label: 'Painel RH', route: '/rh', description: 'Administrar fila de RH.', enabled: this.auth.hasAccess('rh-admin') },
      ];
    }

    if (area === 'ti') {
      return [
        { label: 'Abrir chamado', route: '/ti', description: 'Criar ou acompanhar chamado de TI.', enabled: true },
        { label: 'Base de conhecimento', route: '/ti/base-conhecimento', description: 'Manuais e procedimentos do setor.', enabled: this.auth.hasAccess('base-conhecimento-ti') },
        { label: 'Equipamentos', route: '/ti/equipamentos', description: 'Controle de entregas e retornos.', enabled: this.auth.hasAccess('ti-admin') },
        { label: 'Monitoramento', route: '/ti/monitoramento', description: 'Links, firewalls e disponibilidade.', enabled: this.auth.hasAnyAccess(TI_CHILD_ACCESSES) },
      ];
    }

    if (area === 'compras') {
      return [
        { label: 'Nova compra', route: '/compras', description: 'Solicitar material, serviço ou contratação.', enabled: true },
        { label: 'Aprovacoes', route: '/compras', description: 'Acompanhar diretoria e comprador.', enabled: this.auth.hasAnyRole(['Admin', 'Diretoria']) || this.auth.hasAccess('compras-admin') },
      ];
    }

    if (area === 'controladoria') {
      return [
        { label: 'Guias de ICMS', route: '/controladoria', description: 'Conferir pagamentos e pendencias no Oracle.', enabled: true },
        { label: 'Compras', route: '/compras', description: 'Acompanhar solicitações que impactam pagamentos.', enabled: true },
      ];
    }

    return [
      { label: 'Solicitar RH', route: '/solicitacoes', description: 'Abrir uma demanda administrativa ou de pessoas.', enabled: true },
      { label: 'Abrir chamado TI', route: '/ti', description: 'Acionar suporte técnico.', enabled: true },
      { label: 'Solicitar compra', route: '/compras', description: 'Enviar uma necessidade de compra.', enabled: true },
    ];
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    this.loadDashboard();
    this.loadForecast();
    this.loadUsefulLinks();
    this.loadMonitoringLinks();
    this.startPendingReminder();
  }

  ngOnDestroy(): void {
    this.stopPendingReminder();
  }

  loadDashboard(): void {
    this.loadMonitoringLinks();
    this.loading.set(true);
    void this.spinner.show();
    const canLoadEquipamentos = this.auth.hasAnyAccess(TI_CHILD_ACCESSES);
    forkJoin({
      rh: this.rhService.list().pipe(catchError(() => of([] as SolicitacaoRH[]))),
      ti: this.tiService.list().pipe(catchError(() => of([] as ChamadoTI[]))),
      equipamentos: canLoadEquipamentos ? this.equipamentosService.list().pipe(catchError(() => of([] as EquipamentoTI[]))) : of([] as EquipamentoTI[]),
      compras: this.comprasService.list().pipe(catchError(() => of([] as SolicitacaoCompra[]))),
      users: this.auth.listUsers().pipe(catchError(() => of([] as User[]))),
    }).subscribe({
      next: ({ rh, ti, equipamentos, compras, users }) => {
        this.rhItems.set(rh);
        this.tiItems.set(ti);
        this.equipamentosTi.set(equipamentos);
        this.comprasItems.set(compras);
        this.users.set(users);
        this.atualizadoEm.set(new Date());
        this.loading.set(false);
        void this.spinner.hide();
        this.notifyPendingReminders();
        this.notifyMonitoringOffline();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar os indicadores do dashboard.', 'Dashboard');
      },
    });
  }

  openAction(action: DashboardAction): void {
    if (!action.enabled) {
      this.toastr.warning('Seu perfil não tem acesso a esta área.', 'Acesso restrito');
      return;
    }

    void this.router.navigateByUrl(action.route);
  }

  openAlert(alert: AlertItem): void {
    void this.router.navigateByUrl(alert.route);
  }

  openMetric(metric: MetricCard): void {
    if (!metric.route) {
      return;
    }

    void this.router.navigateByUrl(metric.route);
  }

  printBirthdaysPdf(): void {
    const popup = window.open('', '_blank', 'width=960,height=720');
    if (!popup) {
      this.toastr.warning('Permita pop-ups para gerar o PDF.', 'Aniversariantes');
      return;
    }

    const monthName = new Intl.DateTimeFormat('pt-BR', { month: 'long' }).format(new Date());
    const rows = this.monthBirthdays()
      .map((birthday) => `
        <tr>
          <td>${String(birthday.day).padStart(2, '0')}</td>
          <td>${this.escapeHtml(birthday.nome)}</td>
          <td>${this.escapeHtml(birthday.departamento || '-')}</td>
          <td>${this.escapeHtml(birthday.cargo || '-')}</td>
        </tr>
      `)
      .join('');

    popup.document.write(`
      <html>
        <head>
          <title>Aniversariantes do mes</title>
          <style>
            body { font-family: Arial, sans-serif; margin: 32px; color: #111827; }
            h1 { margin: 0 0 6px; font-size: 24px; }
            p { margin: 0 0 20px; color: #4b5563; }
            table { width: 100%; border-collapse: collapse; }
            th, td { padding: 10px 12px; border: 1px solid #d1d5db; text-align: left; font-size: 13px; }
            th { background: #eef2ff; color: #1f2937; }
          </style>
        </head>
        <body>
          <h1>Aniversariantes de ${this.escapeHtml(monthName)}</h1>
          <p>Emitido em ${new Date().toLocaleDateString('pt-BR')}</p>
          <table>
            <thead>
              <tr>
                <th>Dia</th>
                <th>Nome</th>
                <th>Departamento</th>
                <th>Cargo</th>
              </tr>
            </thead>
            <tbody>${rows || '<tr><td colspan="4">Nenhum aniversariante no mes.</td></tr>'}</tbody>
          </table>
          <script>window.onload = () => { window.print(); };</script>
        </body>
      </html>
    `);
    popup.document.close();
  }

  faviconUrl(link: UsefulLink): string {
    try {
      const domain = new URL(link.url).hostname;
      return `https://www.google.com/s2/favicons?domain=${encodeURIComponent(domain)}&sz=64`;
    } catch {
      return '';
    }
  }

  hideBrokenFavicon(event: Event): void {
    const image = event.target as HTMLImageElement | null;
    image?.setAttribute('hidden', 'true');
  }

  addUsefulLink(): void {
    const label = this.newLinkName().trim();
    const url = this.normalizeUrl(this.newLinkUrl());
    const description = this.newLinkDescription().trim() || 'Link personalizado';
    if (!label || !url) {
      this.toastr.warning('Informe nome e link para adicionar o card.', 'Links úteis');
      return;
    }

    this.usefulLinks.set([...this.usefulLinks(), { label, url, description, icon: 'custom', locked: false }]);
    this.saveUsefulLinks();
    this.newLinkName.set('');
    this.newLinkUrl.set('');
    this.newLinkDescription.set('');
    this.toastr.success('Link adicionado ao dashboard.', 'Links úteis');
  }

  removeUsefulLink(link: UsefulLink, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (link.locked) {
      return;
    }

    this.usefulLinks.set(this.usefulLinks().filter((item) => item !== link));
    this.saveUsefulLinks();
    this.toastr.info('Link removido do dashboard.', 'Links úteis');
  }

  loadForecast(): void {
    this.forecastLoading.set(true);
    this.forecastError.set('');

    const fallback = { latitude: -30.0346, longitude: -51.2177, label: 'Porto Alegre' };
    if (!navigator.geolocation) {
      void this.fetchForecast(fallback.latitude, fallback.longitude, fallback.label);
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        this.forecastLocation.set('Identificando cidade...');
        void this.loadCurrentLocationForecast(position.coords.latitude, position.coords.longitude);
      },
      () => void this.fetchForecast(fallback.latitude, fallback.longitude, fallback.label),
      { timeout: 3500, maximumAge: 30 * 60 * 1000 },
    );
  }

  logout(): void {
    this.auth.logout();
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

  private metric(label: string, value: number | string, detail: string, tone: MetricCard['tone'], route?: string): MetricCard {
    return { label, value, detail, tone, route };
  }

  private defaultMetrics(): MetricCard[] {
    const rh = this.myOpenRhItems();
    const compras = this.myOpenCompraItems();

    return [
      this.metric('Aprovações pendentes', this.pendingApprovals(), 'Chamados de TI fechados aguardando sua aprovação', 'attention'),
      this.metric('Pesquisas pendentes', this.pendingEvaluations(), 'Atendimentos aprovados aguardando avaliação', 'success'),
      this.metric('Minhas solicitações RH', rh.length, 'Solicitações de RH abertas por você', 'attention'),
      this.metric('Minhas compras', compras.length, 'Solicitações de compras abertas por você', 'neutral'),
    ];
  }

  private defaultAlerts(): AlertItem[] {
    return [
      ...this.tiAlerts(this.myTiItems()),
      ...this.rhAlerts(this.myOpenRhItems()),
      ...this.comprasAlerts(this.myOpenCompraItems()),
    ];
  }

  private isTiDepartment(department: string): boolean {
    return department === 'ti'
      || department === 't.i'
      || department.startsWith('ti ')
      || department.endsWith(' ti')
      || department.includes(' tecnologia')
      || department.includes('tecnologia ')
      || department.includes('informatica')
      || department.includes('suporte ti');
  }

  private isRhUser(): boolean {
    const user = this.user();
    const role = this.normalize(user?.role);
    const department = this.normalize(user?.departamento);
    const perfis = (user?.perfis ?? []).map((perfil) => this.normalize(perfil));
    return role === 'rh'
      || role === 'recursos humanos'
      || department === 'rh'
      || department.includes('recursos humanos')
      || perfis.some((perfil) => perfil === 'rh' || perfil === 'recursos humanos')
      || this.auth.hasAnyAccess(['rh-admin', 'dashboard-rh']);
  }

  private loadUsefulLinks(): void {
    const raw = localStorage.getItem(USEFUL_LINKS_KEY);
    if (!raw) {
      return;
    }

    try {
      const customLinks = JSON.parse(raw) as UsefulLink[];
      this.usefulLinks.set([...this.usefulLinks(), ...customLinks.filter((link) => link.label && link.url)]);
    } catch {
      localStorage.removeItem(USEFUL_LINKS_KEY);
    }
  }

  private loadMonitoringLinks(): void {
    try {
      const raw = localStorage.getItem(MONITORING_STORAGE_KEY);
      this.monitoramentoTi.set(raw ? JSON.parse(raw) as MonitorItem[] : []);
    } catch {
      this.monitoramentoTi.set([]);
    }
  }

  private saveUsefulLinks(): void {
    localStorage.setItem(USEFUL_LINKS_KEY, JSON.stringify(this.usefulLinks().filter((link) => !link.locked)));
  }

  private normalizeUrl(value: string): string {
    const trimmed = value.trim();
    if (!trimmed) {
      return '';
    }

    return /^https?:\/\//i.test(trimmed) ? trimmed : `https://${trimmed}`;
  }

  private async loadCurrentLocationForecast(latitude: number, longitude: number): Promise<void> {
    const label = await this.resolveForecastLocation(latitude, longitude);
    await this.fetchForecast(latitude, longitude, label);
  }

  private async resolveForecastLocation(latitude: number, longitude: number): Promise<string> {
    try {
      const params = new URLSearchParams({
        latitude: String(latitude),
        longitude: String(longitude),
        count: '1',
        language: 'pt',
        format: 'json',
      });
      const data = await firstValueFrom(this.http.get<{
        results?: Array<{ name?: string; admin1?: string }>;
      }>(`/api/weather/reverse?${params.toString()}`));
      const result = data.results?.[0];
      const city = result?.name?.trim();
      const state = result?.admin1?.trim();

      if (city && state && city !== state) {
        return `${city}, ${state}`;
      }

      return city || state || 'Sua localizacao';
    } catch {
      return 'Sua localizacao';
    }
  }

  private async fetchForecast(latitude: number, longitude: number, label: string): Promise<void> {
    try {
      const params = new URLSearchParams({
        latitude: String(latitude),
        longitude: String(longitude),
        daily: 'weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max',
        timezone: 'America/Sao_Paulo',
        forecast_days: '4',
      });
      const data = await firstValueFrom(this.http.get<{
        daily?: {
          time?: string[];
          weather_code?: number[];
          temperature_2m_max?: number[];
          temperature_2m_min?: number[];
          precipitation_probability_max?: number[];
        };
      }>(`/api/weather/forecast?${params.toString()}`));

      const days = data.daily?.time ?? [];
      this.forecast.set(days.slice(0, 4).map((date, index) => ({
        date,
        label: index === 0 ? 'Hoje' : this.weekdayLabel(date),
        icon: this.weatherIcon(data.daily?.weather_code?.[index] ?? 0),
        iconTone: this.weatherIconTone(data.daily?.weather_code?.[index] ?? 0),
        temperatureMin: Math.round(data.daily?.temperature_2m_min?.[index] ?? 0),
        temperatureMax: Math.round(data.daily?.temperature_2m_max?.[index] ?? 0),
        rainChance: Math.round(data.daily?.precipitation_probability_max?.[index] ?? 0),
        summary: this.weatherSummary(data.daily?.weather_code?.[index] ?? 0),
      })));
      this.forecastLocation.set(label);
      this.forecastLoading.set(false);
    } catch {
      this.forecast.set([]);
      this.forecastLoading.set(false);
      this.forecastError.set('Não foi possível carregar a previsão agora.');
    }
  }

  private weekdayLabel(value: string): string {
    const date = new Date(`${value}T12:00:00`);
    return date.toLocaleDateString('pt-BR', { weekday: 'short', day: '2-digit' }).replace('.', '');
  }

  private weatherSummary(code: number): string {
    if ([0].includes(code)) return 'Céu limpo';
    if ([1, 2].includes(code)) return 'Parcialmente nublado';
    if ([3, 45, 48].includes(code)) return 'Nublado';
    if ([51, 53, 55, 56, 57].includes(code)) return 'Garoa';
    if ([61, 63, 65, 66, 67, 80, 81, 82].includes(code)) return 'Chuva';
    if ([95, 96, 99].includes(code)) return 'Temporal';
    return 'Tempo instável';
  }

  private weatherIcon(code: number): string {
    if ([0].includes(code)) return '☀';
    if ([1, 2].includes(code)) return '◐';
    if ([3, 45, 48].includes(code)) return '☁';
    if ([51, 53, 55, 56, 57].includes(code)) return '⌁';
    if ([61, 63, 65, 66, 67, 80, 81, 82].includes(code)) return '☂';
    if ([95, 96, 99].includes(code)) return '⚡';
    return '•';
  }

  private weatherIconTone(code: number): string {
    if ([0].includes(code)) return 'sun';
    if ([1, 2].includes(code)) return 'partial';
    if ([3, 45, 48].includes(code)) return 'cloud';
    if ([51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82].includes(code)) return 'rain';
    if ([95, 96, 99].includes(code)) return 'storm';
    return 'neutral';
  }

  private rhScope(): SolicitacaoRH[] {
    return this.auth.hasAccess('rh-admin') ? this.rhItems() : this.myRhItems();
  }

  private tiScope(): ChamadoTI[] {
    return this.auth.hasAccess('ti-admin') ? this.tiItems() : this.myTiItems();
  }

  private comprasScope(): SolicitacaoCompra[] {
    return this.auth.hasAnyRole(['Admin', 'Diretoria']) || this.auth.hasAccess('compras-admin') ? this.comprasItems() : this.myCompraItems();
  }

  private openRh(): SolicitacaoRH[] {
    return this.rhItems().filter((item) => this.isOpenStatus(item.status));
  }

  private openTi(): ChamadoTI[] {
    return this.tiItems().filter((item) => this.isOpenStatus(item.status));
  }

  private openCompras(): SolicitacaoCompra[] {
    return this.comprasItems().filter((item) => !this.isFinalCompra(item));
  }

  private reopenedRh(): SolicitacaoRH[] {
    return this.rhItems().filter((item) => this.isReopened(item.status));
  }

  private reopenedTi(): ChamadoTI[] {
    return this.tiItems().filter((item) => item.reaberto || this.isReopened(item.status));
  }

  private adminQueue(): unknown[] {
    return [
      ...this.rhItems().filter((item) => !item.responsavel?.trim() && this.isOpenStatus(item.status)),
      ...this.tiItems().filter((item) => !item.responsavel?.trim() && this.isOpenStatus(item.status)),
      ...this.comprasItems().filter((item) => item.status === 'Aguardando Diretoria' || item.status.includes('Compras')),
    ];
  }

  private highPriorityAlerts(): unknown[] {
    return [
      ...this.rhItems().filter((item) => this.isHighPriority(item.prioridade)),
      ...this.tiItems().filter((item) => this.isHighPriority(item.prioridade)),
      ...this.comprasItems().filter((item) => this.isHighPriority(item.prioridade)),
    ];
  }

  private myRhItems(): SolicitacaoRH[] {
    const id = this.user()?.id;
    return this.rhItems().filter((item) => item.userid === id);
  }

  private myTiItems(): ChamadoTI[] {
    const id = this.user()?.id;
    return this.tiItems().filter((item) => item.userid === id);
  }

  private myCompraItems(): SolicitacaoCompra[] {
    const id = this.user()?.id;
    return this.comprasItems().filter((item) => item.userid === id);
  }

  private myOpenRhItems(): SolicitacaoRH[] {
    return this.myRhItems().filter((item) => this.isOpenStatus(item.status));
  }

  private myOpenTiItems(): ChamadoTI[] {
    return this.myTiItems().filter((item) => this.isOpenStatus(item.status));
  }

  private myOpenCompraItems(): SolicitacaoCompra[] {
    return this.myCompraItems().filter((item) => !this.isFinalCompra(item));
  }

  private pendingApprovals(): number {
    return this.myTiItems().filter((item) => item.aprovacaoPendente).length;
  }

  private pendingEvaluations(): number {
    return [
      ...this.myRhItems().filter((item) => item.avaliacaoPendente),
      ...this.myTiItems().filter((item) => item.avaliacaoPendente),
    ].length;
  }

  private pendingSatisfactionReminders(): number {
    return this.myTiItems().filter((item) => item.avaliacaoPendente).length;
  }

  private rhAlerts(items: SolicitacaoRH[]): AlertItem[] {
    return items
      .filter((item) => this.isOpenStatus(item.status) || this.isHighPriority(item.prioridade) || this.isReopened(item.status))
      .map((item) => ({
        id: `rh-${item.id}`,
        sector: 'RH',
        title: `#${item.id} ${item.titulo}`,
        detail: `${item.solicitante} - ${item.departamento || 'Sem departamento'}`,
        priority: item.prioridade,
        status: item.status,
        route: this.auth.hasAccess('rh-admin') ? '/rh' : '/solicitacoes',
        date: item.dataSolicitacao,
      }));
  }

  private tiAlerts(items: ChamadoTI[]): AlertItem[] {
    return items
      .filter(
        (item) =>
          this.isOpenStatus(item.status) ||
          this.isHighPriority(item.prioridade) ||
          item.reaberto ||
          this.isReopened(item.status) ||
          item.aprovacaoPendente ||
          item.avaliacaoPendente,
      )
      .map((item) => ({
        id: `ti-${item.id}`,
        sector: 'TI',
        title: `#${item.id} ${item.titulo}`,
        detail: `${item.solicitante} - ${item.categoria || 'Chamado'}`,
        priority: item.aprovacaoPendente ? 'Aguardando aprovação' : item.avaliacaoPendente ? 'Aguardando pesquisa' : item.prioridade,
        status: item.reaberto ? 'Reaberto' : item.status,
        route: '/ti',
        date: item.dataReabertura || item.dataAbertura,
      }));
  }

  private startPendingReminder(): void {
    if (this.reminderIntervalId !== null) {
      return;
    }

    this.reminderIntervalId = setInterval(() => this.notifyPendingReminders(), 5 * 60 * 1000);
  }

  private stopPendingReminder(): void {
    if (this.reminderIntervalId !== null) {
      clearInterval(this.reminderIntervalId);
      this.reminderIntervalId = null;
    }
  }

  private notifyPendingReminders(): void {
    const approvals = this.pendingApprovals();
    const evaluations = this.pendingSatisfactionReminders();

    if (approvals > 0) {
      this.toastr.warning(
        `Você tem ${approvals} chamado(s) de TI aguardando aprovação.`,
        'Aprovação pendente',
        { timeOut: 8000, closeButton: true, progressBar: true },
      );
    }

    if (evaluations > 0) {
      this.toastr.info(
        `Você tem ${evaluations} pesquisa(s) de satisfação pendente(s).`,
        'Pesquisa de satisfação',
        { timeOut: 8000, closeButton: true, progressBar: true },
      );
    }
  }

  private notifyMonitoringOffline(): void {
    if (this.currentArea() !== 'ti') {
      return;
    }

    const offlineCount = this.monitoramentoTi().filter((item) => item.status === 'offline').length;
    if (offlineCount > 0) {
      this.toastr.warning(
        `Existem ${offlineCount} link(s) offline no monitoramento. Verifique e acompanhe os chamados gerados.`,
        'Monitoramento TI',
        { timeOut: 8000, closeButton: true, progressBar: true },
      );
    }
  }

  private monitoramentoAlerts(): AlertItem[] {
    return this.monitoramentoTi()
      .filter((item) => item.status === 'offline')
      .map((item) => ({
        id: `monitoramento-${item.id}`,
        sector: 'TI',
        title: `Link offline: ${item.nome}`,
        detail: `${item.tipo} - ${item.alvo}`,
        priority: 'Offline',
        status: 'Offline',
        route: '/ti/monitoramento',
        date: item.ultimaConsulta || '',
      }));
  }

  private comprasAlerts(items: SolicitacaoCompra[]): AlertItem[] {
    return items
      .filter((item) => !this.isFinalCompra(item) || this.isHighPriority(item.prioridade))
      .map((item) => ({
        id: `compras-${item.id}`,
        sector: 'Compras',
        title: `#${item.id} ${item.titulo}`,
        detail: `${item.solicitante} - ${item.departamento || 'Sem departamento'}`,
        priority: item.prioridade,
        status: item.status,
        route: '/compras',
        date: item.dataSolicitacao,
      }));
  }

  private isOpenStatus(status: string): boolean {
    const normalized = this.normalize(status);
    return !['concluida', 'concluido', 'encerrada', 'encerrado', 'cancelada', 'cancelado', 'reprovada', 'reprovado'].includes(normalized);
  }

  private isCompletedTi(item: ChamadoTI): boolean {
    return !!item.dataEncerramento || this.normalize(item.status) === 'concluido';
  }

  private tiDepartmentCount(items: ChamadoTI[]): number {
    return new Set(items.map((item) => item.departamento?.trim()).filter(Boolean)).size;
  }

  private tiDepartmentSummary(items: ChamadoTI[]): string {
    const grouped = items.reduce<Record<string, number>>((acc, item) => {
      const key = item.departamento?.trim() || 'Sem setor';
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});

    return Object.entries(grouped)
      .sort((a, b) => b[1] - a[1])
      .slice(0, 3)
      .map(([setor, total]) => `${setor}: ${total}`)
      .join(' | ') || 'Sem dados';
  }

  private equipmentUnitSummary(): string {
    const grouped = this.equipamentosTi().reduce<Record<string, number>>((acc, item) => {
      const key = item.destino?.trim() || item.origem?.trim() || 'Sem unidade';
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});

    return Object.entries(grouped)
      .sort((a, b) => b[1] - a[1])
      .slice(0, 3)
      .map(([unidade, total]) => `${unidade}: ${total}`)
      .join(' | ') || 'Sem equipamentos';
  }

  private linkAvailability(_items: ChamadoTI[]): string {
    const links = this.monitoramentoTi();
    if (links.length === 0) {
      return '0%';
    }

    const online = links.filter((item) => item.status === 'online').length;
    const availability = (online / links.length) * 100;

    return `${availability.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;
  }

  private linkAvailabilityDetail(): string {
    const links = this.monitoramentoTi();
    if (links.length === 0) {
      return 'Nenhum link cadastrado no monitoramento';
    }

    const online = links.filter((item) => item.status === 'online').length;
    const offline = links.filter((item) => item.status === 'offline').length;
    return `${online} online | ${offline} offline | ${links.length} cadastrados`;
  }

  private getTiSlaLimitHours(item: ChamadoTI): number {
    const priority = this.normalize(item.prioridade);
    if (priority.includes('alta')) {
      return 8;
    }

    if (priority.includes('baixa')) {
      return 72;
    }

    return 24;
  }

  private getTiSlaElapsedHours(item: ChamadoTI): number {
    const start = new Date(item.dataAbertura).getTime();
    const end = item.dataEncerramento ? new Date(item.dataEncerramento).getTime() : Date.now();
    if (Number.isNaN(start) || Number.isNaN(end)) {
      return 0;
    }

    return Math.max(0, (end - start) / 36e5);
  }

  private isTiSlaMet(item: ChamadoTI): boolean {
    return this.isCompletedTi(item) && this.getTiSlaElapsedHours(item) <= this.getTiSlaLimitHours(item);
  }

  private isTiSlaExpired(item: ChamadoTI): boolean {
    return !this.isTiSlaMet(item) && this.getTiSlaElapsedHours(item) > this.getTiSlaLimitHours(item);
  }

  private isAssignedToCurrentUser(item: ChamadoTI): boolean {
    const user = this.user();
    if (!user) {
      return false;
    }

    const responsible = this.normalize(item.responsavel);
    return !!responsible && [user.nome, user.email].some((value) => this.normalize(value) === responsible);
  }

  private isFinalCompra(item: SolicitacaoCompra): boolean {
    return !!item.dataConclusao || ['Concluida', 'Cancelada', 'Reprovada'].includes(item.status);
  }

  private isHighPriority(priority: string): boolean {
    return ['alta', 'critica', 'crítica', 'urgente'].includes(this.normalize(priority));
  }

  private isReopened(status: string): boolean {
    return this.normalize(status).includes('reabert');
  }

  private priorityWeight(priority: string): number {
    const normalized = this.normalize(priority);
    if (normalized.includes('critic') || normalized.includes('urgente')) {
      return 4;
    }

    if (normalized.includes('alta')) {
      return 3;
    }

    if (normalized.includes('media')) {
      return 2;
    }

    return 1;
  }

  private dateTime(date: string): number {
    const value = new Date(date).getTime();
    return Number.isNaN(value) ? 0 : value;
  }

  private matchesToday(value: string): boolean {
    const date = this.dateParts(value);
    const today = new Date();
    return !!date && date.month === today.getMonth() && date.day === today.getDate();
  }

  private dateParts(value: string): { month: number; day: number } | null {
    if (!value) {
      return null;
    }

    const match = value.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (match) {
      return { month: Number(match[2]) - 1, day: Number(match[3]) };
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return null;
    }

    return { month: date.getMonth(), day: date.getDate() };
  }

  private normalize(value: string | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }
}
