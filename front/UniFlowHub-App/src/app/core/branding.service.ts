import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { Empresa, Unidade } from './models';
import { UnidadesService } from './unidades.service';

const DEFAULT_LOGO = '/uniflowhub-logo.png';
const LOGO_STORAGE_PREFIX = 'uniflowhub.company-logo.';

@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly auth = inject(AuthService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly revendas = signal<Unidade[]>([]);
  private readonly empresas = signal<Empresa[]>([]);
  private readonly logoVersion = signal(0);

  readonly appName = 'UniFlowHub';
  readonly defaultLogo = DEFAULT_LOGO;
  readonly activeCompanyId = computed(() => {
    const unidadeId = this.auth.user()?.unidadeId;
    if (!unidadeId) {
      return null;
    }

    return this.revendas().find((revenda) => revenda.id === unidadeId)?.empresaId ?? null;
  });
  readonly headerLogo = computed(() => {
    this.logoVersion();
    const companyId = this.activeCompanyId();
    return companyId ? this.getCompanyLogo(companyId) : DEFAULT_LOGO;
  });

  constructor() {
    effect(() => {
      if (this.auth.isLoggedIn()) {
        this.loadRevendas();
        this.loadEmpresas();
      }
    });
  }

  getCompanyLogo(companyId: number | null | undefined): string {
    if (!companyId) {
      return DEFAULT_LOGO;
    }

    const company = this.empresas().find((empresa) => empresa.id === companyId);
    if (company) {
      return company.logoUrl || DEFAULT_LOGO;
    }

    if (!this.hasStorage()) {
      return DEFAULT_LOGO;
    }

    return localStorage.getItem(this.storageKey(companyId)) || DEFAULT_LOGO;
  }

  setCompanyLogo(companyId: number, logoDataUrl: string): void {
    if (!this.hasStorage()) {
      return;
    }

    localStorage.setItem(this.storageKey(companyId), logoDataUrl);
    this.logoVersion.update((value) => value + 1);
  }

  clearCompanyLogo(companyId: number): void {
    if (!this.hasStorage()) {
      return;
    }

    localStorage.removeItem(this.storageKey(companyId));
    this.logoVersion.update((value) => value + 1);
  }

  refresh(): void {
    this.loadRevendas();
    this.loadEmpresas();
    this.logoVersion.update((value) => value + 1);
  }

  private loadRevendas(): void {
    this.unidadesService.list().subscribe({
      next: (items) => this.revendas.set(items),
      error: () => this.revendas.set([]),
    });
  }

  private loadEmpresas(): void {
    this.unidadesService.listEmpresas().subscribe({
      next: (items) => this.empresas.set(items),
      error: () => this.empresas.set([]),
    });
  }

  private storageKey(companyId: number): string {
    return `${LOGO_STORAGE_PREFIX}${companyId}`;
  }

  private hasStorage(): boolean {
    return typeof localStorage !== 'undefined';
  }
}
