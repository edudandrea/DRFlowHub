import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { GestaoPessoasService } from '../../core/gestao-pessoas.service';
import { GestaoPessoasCargo } from '../../core/models';
import { ThemeService } from '../../core/theme.service';
import { AcessoSistema, PerfisService } from '../../core/perfis.service';

@Component({
  selector: 'app-controle-acessos',
  imports: [ReactiveFormsModule],
  templateUrl: './controle-acessos.html',
  styleUrl: './controle-acessos.scss',
})
export class ControleAcessosPage implements OnInit {
  private readonly defaultAcessosTi = ['ti', 'ti-admin', 'usuarios', 'empresas-revendas', 'base-conhecimento-ti', 'equipamentos-ti'];
  private readonly defaultAcessosRh = ['rh', 'rh-admin', 'gestao-pessoas', 'gestao-pessoas-admin', 'cartao-ponto'];
  private readonly defaultAcessosTiAdmin = ['ti-admin', 'base-conhecimento-ti', 'equipamentos-ti'];
  private readonly defaultAcessosRhAdmin = ['rh-admin'];
  private readonly hiddenAcessosTiAdmin = ['base-conhecimento-ti', 'equipamentos-ti'];
  
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly service = inject(GestaoPessoasService);
  private readonly toastr = inject(ToastrService);
  private readonly perfisService = inject(PerfisService);

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly cargosCadastro = signal<GestaoPessoasCargo[]>([]);
  readonly acessosDisponiveis = signal<AcessoSistema[]>([]);
  readonly selectedAcessoCargo = signal<GestaoPessoasCargo | null>(null);
  readonly acessosCargoDraft = signal<string[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly modalAcessosOpen = signal(false);
  readonly profileMenuOpen = signal(false);
  readonly search = signal('');

  readonly canManageAcessos = computed(() => this.auth.hasAnyAccess(['usuarios', 'empresas-revendas']) || this.auth.hasAnyRole(['Admin', 'TI']));
  readonly filteredCargos = computed(() => {
    const term = this.normalize(this.search());
    return this.cargosCadastro().filter((item) => {
      return !term || [
        item.nome,
        item.departamento,
      ].some((value) => this.normalize(value).includes(term));
    });
  });

  ngOnInit(): void {
    this.loadCargos();
    this.loadAcessos();
  }

  private loadCargos(): void {
    this.loading.set(true);
    this.service.listCargos().subscribe({
      next: (cargos) => {
        this.cargosCadastro.set(cargos);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toastr.error('Nao foi possivel carregar os cargos.', 'Erro');
      },
    });
  }

  private loadAcessos(): void {
    this.perfisService.listAcessos().subscribe({
      next: (acessos) => this.acessosDisponiveis.set(acessos.filter((acesso) => acesso.chave !== 'perfis')),
      error: () => this.acessosDisponiveis.set([]),
    });
  }

  goHome(): void {
    this.router.navigate(['/hub']);
  }

  openAcessosCargo(cargo: GestaoPessoasCargo): void {
    const current = this.cargosCadastro().find((item) => item.id === cargo.id) ?? cargo;
    this.selectedAcessoCargo.set(current);
    this.acessosCargoDraft.set(this.acessosEfetivosCargo(current));
    this.modalAcessosOpen.set(true);
  }

  toggleAcessoCargo(chave: string): void {
    const current = this.acessosCargoDraft();
    this.acessosCargoDraft.set(current.includes(chave) ? current.filter((item) => item !== chave) : [...current, chave]);
  }

  hasAcessoCargo(chave: string): boolean {
    return this.acessosCargoDraft().includes(chave);
  }

  acessoNome(chave: string): string {
    return this.acessosDisponiveis().find((item) => item.chave === chave)?.nome ?? chave;
  }

  acessosEfetivosCargo(cargo: GestaoPessoasCargo | null | undefined): string[] {
    if (!cargo) {
      return [];
    }

    const acessos = new Set<string>(cargo.acessos ?? []);
    const cargoText = `${cargo.nome} ${cargo.departamento}`;
    if (this.isTiText(cargoText)) {
      this.defaultAcessosTi.forEach((acesso) => acessos.add(acesso));
    }
    if (this.isRhText(cargoText)) {
      this.defaultAcessosRh.forEach((acesso) => acessos.add(acesso));
    }

    return Array.from(acessos).sort((a, b) => this.acessoNome(a).localeCompare(this.acessoNome(b)));
  }

  isAcessoPadraoCargo(chave: string): boolean {
    const cargo = this.selectedAcessoCargo();
    if (!cargo) {
      return false;
    }

    const cargoText = `${cargo.nome} ${cargo.departamento}`;
    return (this.isTiText(cargoText) && this.defaultAcessosTiAdmin.includes(chave))
      || (this.isRhText(cargoText) && this.defaultAcessosRhAdmin.includes(chave));
  }

  isHiddenAcessoCargo(chave: string): boolean {
    const cargo = this.selectedAcessoCargo();
    if (!cargo) {
      return false;
    }

    const cargoText = `${cargo.nome} ${cargo.departamento}`;
    return this.isTiText(cargoText)
      && cargo.acessos.includes('ti-admin')
      && this.hiddenAcessosTiAdmin.includes(chave);
  }

  submitAcessosCargo(): void {
    const cargo = this.selectedAcessoCargo();
    if (!cargo || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.service.saveCargo({
      nome: cargo.nome,
      departamento: cargo.departamento,
      descricao: cargo.descricao,
      ativo: cargo.ativo,
      itens: cargo.itens.map((item) => ({ itemId: item.itemId, quantidade: item.quantidade, obrigatorio: item.obrigatorio })),
      acessos: this.acessosCargoDraft(),
    }, cargo.id).subscribe({
      next: (saved) => {
        this.cargosCadastro.set(this.cargosCadastro().map((item) => item.id === saved.id ? saved : item));
        this.selectedAcessoCargo.set(saved);
        this.saving.set(false);
        this.modalAcessosOpen.set(false);
        this.toastr.success('Acessos do cargo atualizados.', 'Controle de acessos');
      },
      error: (error) => {
        this.saving.set(false);
        this.toastr.error(error?.error || 'Nao foi possivel salvar os acessos do cargo.', 'Erro');
      },
    });
  }

  editProfile(): void {
    this.router.navigate(['/usuarios']);
  }

  changePassword(): void {
    this.router.navigate(['/usuarios']);
  }

  logout(): void {
    this.auth.logout();
  }

  private normalize(value: string | number | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  private isTiText(value: string): boolean {
    const normalized = ` ${this.normalize(value)} `;
    return normalized.includes(' ti ')
      || normalized.includes(' t.i ')
      || normalized.includes(' tecnologia ');
  }

  private isRhText(value: string): boolean {
    const normalized = ` ${this.normalize(value)} `;
    return normalized.includes(' rh ')
      || normalized.includes(' recursos humanos ');
  }
}
