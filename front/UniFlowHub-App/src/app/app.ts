import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { NgxSpinnerModule } from 'ngx-spinner';
import { filter } from 'rxjs';
import { AuthService } from './core/auth.service';
import { ChatAlertService } from './core/chat-alert.service';
import { BrandingService } from './core/branding.service';
import { ProfileDialogComponent } from './core/profile-dialog.component';
import { ThemeService } from './core/theme.service';
import { Role } from './core/models';

interface ShellLink {
  label: string;
  description: string;
  route: string;
  enabled: boolean;
  roles?: Role[];
  access?: string | string[];
  adminAccess?: string;
  hiddenForRoles?: Role[];
  userRoute?: string;
  children?: ShellLink[];
}

const collator = new Intl.Collator('pt-BR', { sensitivity: 'base' });

const SPINNER_BACKDROP_BY_ROLE: Record<Role, string> = {
  Admin: 'rgba(2, 44, 65, 0.82)',
  RH: 'rgba(3, 69, 82, 0.78)',
  TI: 'rgba(3, 82, 114, 0.78)',
  Diretoria: 'rgba(5, 46, 86, 0.8)',
  Compras: 'rgba(5, 86, 70, 0.78)',
  Controladoria: 'rgba(4, 65, 86, 0.78)',
  'Qualidade Nissan': 'rgba(3, 74, 96, 0.78)',
  'Gerente Geral de Pecas': 'rgba(3, 86, 70, 0.78)',
  'Gerente de Pecas': 'rgba(5, 80, 74, 0.78)',
  'Vendedor de Pecas': 'rgba(4, 66, 96, 0.78)',
  Gestor: 'rgba(5, 53, 94, 0.78)',
  Usuario: 'rgba(4, 43, 65, 0.76)',
};

const PECAS_BI_ACCESSES = [
  'pecas-admin',
  'vendas-pecas',
  'pecas-bi-renault',
  'pecas-bi-nissan',
  'pecas-bi-gm',
  'pecas-bi-fiat',
  'pecas-bi-bajaj',
  'pecas-bi-peugeot-citroen',
  'pecas-bi-mg',
  'pecas-bi-geely',
];
const TI_CHILD_ACCESSES = ['ti-admin', 'base-conhecimento-ti', 'equipamentos-ti'];

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ProfileDialogComponent, NgxSpinnerModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly theme = inject(ThemeService);
  readonly branding = inject(BrandingService);
  readonly chatAlert = inject(ChatAlertService);

  readonly currentUrl = signal(this.router.url);
  readonly expandedMenus = signal<string[]>([]);
  readonly showSidebar = computed(() => this.auth.isLoggedIn() && !this.currentUrl().startsWith('/login'));
  readonly spinnerBackdropColor = computed(() => {
    const role = this.auth.user()?.role;
    return role ? SPINNER_BACKDROP_BY_ROLE[role] : SPINNER_BACKDROP_BY_ROLE['Usuario'];
  });

  readonly shellLinks: ShellLink[] = [
    { label: 'Dashboard', description: 'Visão inicial', route: '/hub', enabled: true },
    {
      label: 'Gestão de Pessoas',
      description: 'Admissão, demissão e RH',
      route: '/rh',
      enabled: true,
      children: [
        { label: 'Admissão e Demissão', description: 'Fluxo direto do RH', route: '/gestao-pessoas', enabled: true, access: 'rh-admin' },
        { label: 'Solicitações do RH', description: 'Atendimento e demandas', route: '/rh', userRoute: '/solicitacoes', enabled: true, access: 'rh', adminAccess: 'rh-admin' },
        { label: 'Controle Cartão Ponto', description: 'Espelho e ajustes', route: '/rh/cartao-ponto', enabled: true, access: 'cartao-ponto' },
      ],
    },
    {
      label: 'TI',
      description: 'Chamados e suporte',
      route: '/ti',
      enabled: true,
      children: [
        { label: 'Base de conhecimento', description: 'Manuais e procedimentos', route: '/ti/base-conhecimento', enabled: true, access: 'base-conhecimento-ti' },
        { label: 'Chamados', description: 'Fila e atendimento', route: '/ti', enabled: true, access: 'ti', adminAccess: 'ti-admin' },
        { label: 'Controle de equipamentos', description: 'Gestão e movimentações', route: '/ti/equipamentos', enabled: true, access: 'equipamentos-ti' },
        { label: 'Inventário de equipamentos', description: 'Cadastro, usuário e QR Code', route: '/ti/equipamentos/inventario', enabled: true, access: 'equipamentos-ti' },
        { label: 'Monitoramento', description: 'Links e firewalls', route: '/ti/monitoramento', enabled: true, access: TI_CHILD_ACCESSES },
      ],
    },
    { label: 'Compras', description: 'Solicitações e aprovação', route: '/compras', enabled: true, access: 'compras', adminAccess: 'compras-admin' },
    { label: 'Controladoria', description: 'Guias de ICMS', route: '/controladoria', enabled: true, access: 'controladoria', hiddenForRoles: ['Gerente Geral de Pecas', 'Gerente de Pecas', 'Vendedor de Pecas'] },
    {
      label: 'Veículos',
      description: 'Veículos e reservas',
      route: '/estoque',
      enabled: true,
      hiddenForRoles: ['Gerente Geral de Pecas', 'Gerente de Pecas', 'Vendedor de Pecas'],
      children: [
        { label: 'Estoque', description: 'Consulta de chassi e reserva', route: '/estoque/veiculos', enabled: true, access: 'veiculos' },
        { label: 'BI Venda de Veículos', description: 'Indicadores comerciais', route: '/veiculos/bi-vendas', enabled: true, access: 'veiculos-bi' },
        { label: 'Repasse', description: 'Análise de estoque usado', route: '/veiculos/repasses', enabled: true, access: 'veiculos-repasses' },
      ],
    },
    { label: 'Vendas Peças', description: 'B.I comercial de pecas', route: '/vendas-pecas', enabled: true, access: PECAS_BI_ACCESSES },
    { label: 'Financeiro', description: 'Fluxo financeiro', route: '/hub', enabled: false },
    { label: 'Administrativo', description: 'Demandas internas', route: '/hub', enabled: false },
    { label: 'Operacional', description: 'Solicitações operacionais', route: '/hub', enabled: false },
    { label: 'Comercial', description: 'Demandas comerciais', route: '/hub', enabled: false },
    {
      label: 'Cadastros',
      description: 'Usuários, empresas e perfis',
      route: '/cadastros',
      enabled: true,
      children: [
        { label: 'Usuários', description: 'Administração de acessos', route: '/usuarios', enabled: true, access: 'usuarios' },
        { label: 'Empresas e Revendas', description: 'Cadastro operacional', route: '/cadastros/empresas-revendas', enabled: true, access: 'empresas-revendas' },
        { label: 'Cadastro de perfil', description: 'Perfis e acessos', route: '/cadastros/perfis', enabled: true, access: 'perfis' },
      ],
    },
  ];
  readonly visibleShellLinks = computed(() => this.sortShellLinks(this.shellLinks.filter((link) => this.canShowParentLink(link))));

  constructor() {
    effect(() => {
      if (this.auth.isLoggedIn()) {
        void this.chatAlert.start();
      } else {
        void this.chatAlert.stop();
      }
    });

    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((event) => this.currentUrl.set(event.urlAfterRedirects));
  }

  openShellLink(link: ShellLink): void {
    if (!link.enabled) {
      return;
    }

    if (this.visibleChildren(link).length > 0) {
      this.toggleMenu(link);
      return;
    }

    const route = this.resolveRoute(link);
    if (!route) {
      return;
    }

    void this.router.navigateByUrl(route);
  }

  isActive(link: ShellLink): boolean {
    const url = this.currentUrl();
    const route = this.resolveRoute(link);

    if (!route) {
      return false;
    }

    if (url === route) {
      return true;
    }

    if (route === '/hub' || !url.startsWith(`${route}/`)) {
      return false;
    }

    return !this.hasMoreSpecificVisibleRoute(route, url);
  }

  visibleChildren(link: ShellLink): ShellLink[] {
    return this.sortShellLinks(link.children?.filter((child) => this.canShowChildLink(child)) ?? [], false);
  }

  hasActiveChild(link: ShellLink): boolean {
    return this.visibleChildren(link).some((child) => this.isActive(child));
  }

  isExpanded(link: ShellLink): boolean {
    return this.expandedMenus().includes(link.label);
  }

  private toggleMenu(link: ShellLink): void {
    const expanded = this.expandedMenus();
    this.expandedMenus.set(
      expanded.includes(link.label)
        ? expanded.filter((label) => label !== link.label)
        : [...expanded, link.label],
    );
  }

  private canShowParentLink(link: ShellLink): boolean {
    if (link.hiddenForRoles && this.auth.hasAnyRole(link.hiddenForRoles)) {
      return false;
    }

    if (link.access) {
      return this.hasLinkAccess(link.access);
    }

    if (link.children?.length) {
      return this.visibleChildren(link).length > 0;
    }

    return !link.roles || this.auth.hasAnyRole(link.roles) || !!link.userRoute;
  }

  private canShowChildLink(link: ShellLink): boolean {
    if (link.hiddenForRoles && this.auth.hasAnyRole(link.hiddenForRoles)) {
      return false;
    }

    if (link.access) {
      return this.hasLinkAccess(link.access);
    }

    return !link.roles || this.auth.hasAnyRole(link.roles) || !!link.userRoute;
  }

  private resolveRoute(link: ShellLink): string {
    if (link.adminAccess && link.userRoute && !this.auth.hasAccess(link.adminAccess)) {
      return link.userRoute;
    }

    return link.roles && !this.auth.hasAnyRole(link.roles) ? (link.userRoute ?? link.route) : link.route;
  }

  private hasLinkAccess(access: string | string[]): boolean {
    return Array.isArray(access) ? this.auth.hasAnyAccess(access) : this.auth.hasAccess(access);
  }

  private hasMoreSpecificVisibleRoute(route: string, url: string): boolean {
    return this.flattenLinks(this.shellLinks)
      .filter((link) => this.canShowRouteCandidate(link))
      .map((link) => this.resolveRoute(link))
      .some((candidate) => candidate !== route && candidate.startsWith(`${route}/`) && url.startsWith(candidate));
  }

  private flattenLinks(links: ShellLink[]): ShellLink[] {
    return links.flatMap((link) => [link, ...this.flattenLinks(link.children ?? [])]);
  }

  private canShowRouteCandidate(link: ShellLink): boolean {
    return link.children?.length ? this.canShowParentLink(link) : this.canShowChildLink(link);
  }

  private sortShellLinks(links: ShellLink[], keepDashboardFirst = true): ShellLink[] {
    const sorted = links.slice().sort((a, b) => collator.compare(a.label, b.label));

    if (!keepDashboardFirst) {
      return sorted;
    }

    const dashboard = sorted.find((link) => link.label === 'Dashboard');
    return dashboard ? [dashboard, ...sorted.filter((link) => link !== dashboard)] : sorted;
  }
}


