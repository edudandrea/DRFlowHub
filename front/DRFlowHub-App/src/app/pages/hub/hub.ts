import { isPlatformBrowser } from '@angular/common';
import { Component, HostListener, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { ChamadosTIService } from '../../core/chamados-ti.service';
import { ComprasService } from '../../core/compras.service';
import { ChamadoTI, SolicitacaoCompra, SolicitacaoRH, User } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { SolicitacoesService } from '../../core/solicitacoes.service';
import { ThemeService } from '../../core/theme.service';

type DashboardArea = 'admin' | 'rh' | 'ti' | 'compras' | 'controladoria' | 'financeiro' | 'departamento';

interface MetricCard {
  label: string;
  value: number;
  detail: string;
  tone: 'neutral' | 'attention' | 'danger' | 'success';
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
  imports: [],
  templateUrl: './hub.html',
  styleUrl: './hub.scss',
})
export class HubPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly rhService = inject(SolicitacoesService);
  private readonly tiService = inject(ChamadosTIService);
  private readonly comprasService = inject(ComprasService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly profileMenuOpen = signal(false);
  readonly loading = signal(false);
  readonly rhItems = signal<SolicitacaoRH[]>([]);
  readonly tiItems = signal<ChamadoTI[]>([]);
  readonly comprasItems = signal<SolicitacaoCompra[]>([]);
  readonly users = signal<User[]>([]);
  readonly forecast = signal<ForecastDay[]>([]);
  readonly forecastLocation = signal('Porto Alegre');
  readonly forecastLoading = signal(false);
  readonly forecastError = signal('');

  readonly greeting = computed(() => {
    const firstName = (this.user()?.nome || 'Usuario').trim().split(/\s+/)[0];
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

    if (role === 'RH' || department.includes('rh') || department.includes('recursos humanos')) {
      return 'rh';
    }

    if (role === 'TI' || department.includes('ti') || department.includes('tecnologia')) {
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

    return 'departamento';
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

    return `Dashboard de ${user?.departamento || 'departamento'}`;
  });

  readonly dashboardSubtitle = computed(() => {
    const user = this.user();
    if (this.currentArea() === 'admin') {
      return 'Acompanhe os alertas dos setores ativos e entre nas administrações quando precisar agir.';
    }

    return `${user?.nome || 'Usuario'}, estes são os alertas priorizados para seu perfil e setor.`;
  });

  readonly isBirthdayToday = computed(() => {
    const birthDate = this.user()?.dataNascimento;
    return !!birthDate && this.matchesToday(birthDate);
  });

  readonly birthdayGreeting = computed(() => {
    const firstName = (this.user()?.nome || 'voce').trim().split(/\s+/)[0];
    return `Feliz aniversario, ${firstName}! Que seu dia seja leve, especial e cheio de boas notícias.`;
  });

  readonly showBirthdays = computed(() => this.currentArea() === 'rh' && this.auth.hasAccess('rh-admin'));

  readonly monthBirthdays = computed<BirthdayItem[]>(() => {
    const today = new Date();
    const currentMonth = today.getMonth();

    return this.users()
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
        this.metric('Prioridade alta', this.highPriorityAlerts().length, 'Solicitações criticas ou altas', 'danger'),
        this.metric('Reabertas', this.reopenedRh().length + this.reopenedTi().length, 'Itens que voltaram ao fluxo', 'danger'),
        this.metric('Administracao', this.adminQueue().length, 'Pendencias para responsáveis', 'neutral'),
      ];
    }

    if (area === 'rh') {
      const items = this.rhScope();
      return [
        this.metric('Abertas', items.filter((item) => this.isOpenStatus(item.status)).length, 'Solicitações em atendimento', 'attention'),
        this.metric('Reabertas', items.filter((item) => this.isReopened(item.status)).length, 'Retornaram para análise', 'danger'),
        this.metric('Alta prioridade', items.filter((item) => this.isHighPriority(item.prioridade)).length, 'Demandas sensíveis', 'danger'),
        this.metric('Sem responsavel', items.filter((item) => !item.responsavel?.trim()).length, 'Precisam de triagem', 'neutral'),
      ];
    }

    if (area === 'ti') {
      const items = this.tiScope();
      return [
        this.metric('Abertos', items.filter((item) => this.isOpenStatus(item.status)).length, 'Chamados ativos', 'attention'),
        this.metric('Reabertos', items.filter((item) => item.reaberto || this.isReopened(item.status)).length, 'Voltaram para suporte', 'danger'),
        this.metric('Alta prioridade', items.filter((item) => this.isHighPriority(item.prioridade)).length, 'Incidentes relevantes', 'danger'),
        this.metric('Sem responsavel', items.filter((item) => !item.responsavel?.trim()).length, 'Aguardando atribuição', 'neutral'),
      ];
    }

    if (area === 'compras') {
      const items = this.comprasScope();
      return [
        this.metric('Aguardando diretoria', items.filter((item) => item.status === 'Aguardando Diretoria').length, 'Pendentes de aprovacao', 'attention'),
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

    return [
      this.metric('Minhas solicitacoes RH', this.myRhItems().filter((item) => this.isOpenStatus(item.status)).length, 'Demandas abertas por voce', 'attention'),
      this.metric('Meus chamados TI', this.myTiItems().filter((item) => this.isOpenStatus(item.status)).length, 'Chamados em aberto', 'attention'),
      this.metric('Minhas compras', this.myCompraItems().filter((item) => !this.isFinalCompra(item)).length, 'Solicitações em andamento', 'neutral'),
      this.metric('Avaliacoes pendentes', this.pendingEvaluations(), 'Atendimentos encerrados a avaliar', 'success'),
    ];
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
      alerts = this.tiAlerts(this.tiScope());
    } else if (area === 'compras') {
      alerts = this.comprasAlerts(this.comprasScope());
    } else {
      alerts = [
        ...this.rhAlerts(this.myRhItems()),
        ...this.tiAlerts(this.myTiItems()),
        ...this.comprasAlerts(this.myCompraItems()),
      ];
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
        { label: 'Usuarios', route: '/usuarios', description: 'Crie acessos e ajuste perfis.', enabled: this.auth.hasAnyRole(['Admin', 'TI']) },
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
        { label: 'Equipamentos', route: '/ti/equipamentos', description: 'Controle de entregas e retornos.', enabled: this.auth.hasAccess('ti-admin') },
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
        { label: 'Compras', route: '/compras', description: 'Acompanhar solicitacoes que impactam pagamentos.', enabled: true },
      ];
    }

    return [
      { label: 'Solicitar RH', route: '/solicitacoes', description: 'Abrir uma demanda administrativa ou de pessoas.', enabled: true },
      { label: 'Chamado TI', route: '/ti', description: 'Acionar suporte técnico.', enabled: true },
      { label: 'Solicitar compra', route: '/compras', description: 'Enviar uma necessidade de compra.', enabled: true },
    ];
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    this.loadDashboard();
    this.loadForecast();
  }

  loadDashboard(): void {
    this.loading.set(true);
    void this.spinner.show();
    forkJoin({
      rh: this.rhService.list().pipe(catchError(() => of([] as SolicitacaoRH[]))),
      ti: this.tiService.list().pipe(catchError(() => of([] as ChamadoTI[]))),
      compras: this.comprasService.list().pipe(catchError(() => of([] as SolicitacaoCompra[]))),
      users: this.auth.listUsers().pipe(catchError(() => of([] as User[]))),
    }).subscribe({
      next: ({ rh, ti, compras, users }) => {
        this.rhItems.set(rh);
        this.tiItems.set(ti);
        this.comprasItems.set(compras);
        this.users.set(users);
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Nao foi possivel carregar os indicadores do dashboard.', 'Dashboard');
      },
    });
  }

  openAction(action: DashboardAction): void {
    if (!action.enabled) {
      this.toastr.warning('Seu perfil nao tem acesso a esta area.', 'Acesso restrito');
      return;
    }

    void this.router.navigateByUrl(action.route);
  }

  openAlert(alert: AlertItem): void {
    void this.router.navigateByUrl(alert.route);
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
        this.forecastLocation.set('Sua localizacao');
        void this.fetchForecast(position.coords.latitude, position.coords.longitude, 'Sua localizacao');
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

  private metric(label: string, value: number, detail: string, tone: MetricCard['tone']): MetricCard {
    return { label, value, detail, tone };
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
      const response = await fetch(`https://api.open-meteo.com/v1/forecast?${params.toString()}`);
      if (!response.ok) {
        throw new Error('Forecast unavailable');
      }

      const data = await response.json() as {
        daily?: {
          time?: string[];
          weather_code?: number[];
          temperature_2m_max?: number[];
          temperature_2m_min?: number[];
          precipitation_probability_max?: number[];
        };
      };

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
      this.forecastError.set('Nao foi possivel carregar a previsao agora.');
    }
  }

  private weekdayLabel(value: string): string {
    const date = new Date(`${value}T12:00:00`);
    return date.toLocaleDateString('pt-BR', { weekday: 'short', day: '2-digit' }).replace('.', '');
  }

  private weatherSummary(code: number): string {
    if ([0].includes(code)) return 'Ceu limpo';
    if ([1, 2].includes(code)) return 'Parcialmente nublado';
    if ([3, 45, 48].includes(code)) return 'Nublado';
    if ([51, 53, 55, 56, 57].includes(code)) return 'Garoa';
    if ([61, 63, 65, 66, 67, 80, 81, 82].includes(code)) return 'Chuva';
    if ([95, 96, 99].includes(code)) return 'Temporal';
    return 'Tempo instavel';
  }

  private weatherIcon(code: number): string {
    if ([0].includes(code)) return '☀';
    if ([1, 2].includes(code)) return '◐';
    if ([3, 45, 48].includes(code)) return '☁';
    if ([51, 53, 55, 56, 57].includes(code)) return '☂';
    if ([61, 63, 65, 66, 67, 80, 81, 82].includes(code)) return '☔';
    if ([95, 96, 99].includes(code)) return '⚡';
    return '◌';
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

  private pendingEvaluations(): number {
    return [
      ...this.myRhItems().filter((item) => item.avaliacaoPendente),
      ...this.myTiItems().filter((item) => item.avaliacaoPendente),
    ].length;
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
      .filter((item) => this.isOpenStatus(item.status) || this.isHighPriority(item.prioridade) || item.reaberto || this.isReopened(item.status))
      .map((item) => ({
        id: `ti-${item.id}`,
        sector: 'TI',
        title: `#${item.id} ${item.titulo}`,
        detail: `${item.solicitante} - ${item.categoria || 'Chamado'}`,
        priority: item.prioridade,
        status: item.reaberto ? 'Reaberto' : item.status,
        route: '/ti',
        date: item.dataReabertura || item.dataAbertura,
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
}
