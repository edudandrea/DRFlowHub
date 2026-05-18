import { isPlatformBrowser } from '@angular/common';
import { Component, HostListener, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { Empresa, Role, Unidade, User, UserCreatePayload, UserUpdatePayload } from '../../core/models';
import { ThemeService } from '../../core/theme.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { PerfisService } from '../../core/perfis.service';
import { UnidadesService } from '../../core/unidades.service';
import { toDateInputValue } from '../../core/date-utils';

type UserModalMode = 'create' | 'edit';
type UnidadeModalMode = 'create' | 'edit';
type EmpresaModalMode = 'create' | 'edit';
type UserSortField = 'nome' | 'email' | 'role' | 'departamento' | 'cargo' | 'unidadeNome';
type UnidadeSortField = 'empresa' | 'empresaNumero' | 'revenda' | 'numeroRevenda' | 'cnpj';
type EmpresaSortField = 'nome' | 'numero';

interface UserRevendaGroup {
  key: string;
  label: string;
  users: User[];
  totalAtivos: number;
}

interface UserEmpresaGroup {
  key: string;
  label: string;
  totalUsuarios: number;
  totalAtivos: number;
  revendas: UserRevendaGroup[];
}

@Component({
  selector: 'app-usuarios',
  imports: [ReactiveFormsModule],
  templateUrl: './usuarios.html',
  styleUrl: './usuarios.scss',
})
export class UsuariosPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly perfisService = inject(PerfisService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly users = signal<User[]>([]);
  readonly empresasCadastro = signal<Empresa[]>([]);
  readonly unidades = signal<Unidade[]>([]);
  readonly selectedUnidade = signal<Unidade | null>(null);
  readonly selectedEmpresa = signal<Empresa | null>(null);
  readonly selected = signal<User | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly modalOpen = signal(false);
  readonly modalMode = signal<UserModalMode>('create');
  readonly unidadeModalOpen = signal(false);
  readonly unidadeModalMode = signal<UnidadeModalMode>('create');
  readonly empresaModalOpen = signal(false);
  readonly empresaModalMode = signal<EmpresaModalMode>('create');
  readonly search = signal('');
  readonly unidadeSearch = signal('');
  readonly profileMenuOpen = signal(false);
  readonly userPage = signal(1);
  readonly unidadePage = signal(1);
  readonly empresaPage = signal(1);
  readonly pageSize = signal(10);
  readonly userSortField = signal<UserSortField>('nome');
  readonly unidadeSortField = signal<UnidadeSortField>('empresa');
  readonly empresaSortField = signal<EmpresaSortField>('numero');
  readonly sortDirection = signal<'asc' | 'desc'>('asc');

  readonly roles = signal<Role[]>(['Admin', 'RH', 'TI', 'Diretoria', 'Compras', 'Controladoria', 'Qualidade Nissan', 'Gerente Geral de Pecas', 'Gerente de Pecas', 'Vendedor de Pecas', 'Gestor', 'Usuario']);
  readonly departamentos = ['Administrativo', 'RH', 'TI', 'Financeiro', 'Controladoria', 'Compras', 'Qualidade Nissan', 'Pecas', 'Operacional', 'Comercial'];

  readonly totalAdmins = computed(() => this.users().filter((item) => item.role === 'Admin').length);
  readonly totalAtivos = computed(() => this.users().filter((item) => item.ativo).length);
  readonly empresas = computed(() => this.empresasCadastro().slice().sort((a, b) => a.numero - b.numero || a.nome.localeCompare(b.nome)));
  readonly filtered = computed(() => {
    const term = this.normalize(this.search());
    if (!term) {
      return this.sortItems(this.users(), this.userSortField());
    }

    return this.sortItems(this.users().filter((item) =>
      [item.nome, item.cpf, item.email, item.role, item.departamento, item.cargo, item.unidadeNome, item.ativo ? 'ativo' : 'inativo']
        .some((value) => this.normalize(value).includes(term)),
    ), this.userSortField());
  });
  readonly filteredUnidades = computed(() => {
    const term = this.normalize(this.unidadeSearch());
    return this.sortItems(this.unidades().filter((item) => !term || [item.empresaNumero, item.empresa, item.numeroRevenda, item.revenda, item.nome, item.cnpj, item.endereco].some((value) => this.normalize(value).includes(term))), this.unidadeSortField());
  });
  readonly sortedEmpresas = computed(() => this.sortItems(this.empresas(), this.empresaSortField()));
  readonly userTree = computed(() => this.buildUserTree(this.filtered()));
  readonly totalUserPages = computed(() => this.totalPages(this.filtered().length));
  readonly totalUnidadePages = computed(() => this.totalPages(this.filteredUnidades().length));
  readonly totalEmpresaPages = computed(() => this.totalPages(this.sortedEmpresas().length));
  readonly pagedUsers = computed(() => this.pageItems(this.filtered(), this.userPage()));
  readonly pagedUnidades = computed(() => this.pageItems(this.filteredUnidades(), this.unidadePage()));
  readonly pagedEmpresas = computed(() => this.pageItems(this.sortedEmpresas(), this.empresaPage()));

  readonly form = this.fb.nonNullable.group({
    nome: ['', Validators.required],
    cpf: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    role: ['Usuario', Validators.required],
    departamento: ['', Validators.required],
    cargo: ['', Validators.required],
    empresaSelecionadaId: [0],
    unidadeId: [0],
    ativo: [true],
    dataNascimento: ['1990-01-01', Validators.required],
    senha: [''],
  });

  readonly unidadeForm = this.fb.nonNullable.group({
    empresaId: [0, [Validators.required, Validators.min(1)]],
    numeroRevenda: [0, [Validators.required, Validators.min(1)]],
    revenda: ['', Validators.required],
    cnpj: ['', Validators.required],
    endereco: ['', Validators.required],
  });

  readonly empresaForm = this.fb.nonNullable.group({
    numero: [0, [Validators.required, Validators.min(1)]],
    nome: ['', Validators.required],
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    this.load();
    this.loadPerfis();
    this.loadEmpresas();
    this.loadUnidades();
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.auth.listUsers().subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: (error) => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error(this.getErrorMessage('Nao foi possivel carregar os usuarios.', error), 'Erro');
      },
    });
  }

  loadUnidades(): void {
    this.unidadesService.list().subscribe({
      next: (unidades) => this.unidades.set(unidades),
      error: (error) => this.toastr.error(this.getErrorMessage('Nao foi possivel carregar as empresas e revendas.', error), 'Erro'),
    });
  }

  loadEmpresas(): void {
    this.unidadesService.listEmpresas().subscribe({
      next: (empresas) => this.empresasCadastro.set(empresas),
      error: (error) => this.toastr.error(this.getErrorMessage('Nao foi possivel carregar as empresas.', error), 'Erro'),
    });
  }

  loadPerfis(): void {
    this.perfisService.list().subscribe({
      next: (perfis) => this.roles.set(perfis.map((perfil) => perfil.nome)),
      error: () => this.toastr.error('Nao foi possivel carregar os perfis.', 'Usuarios'),
    });
  }

  openCreate(): void {
    this.modalMode.set('create');
    this.selected.set(null);
    this.form.reset({
      nome: '',
      cpf: '',
      email: '',
      role: 'Usuario',
      departamento: '',
      cargo: '',
      empresaSelecionadaId: 0,
      unidadeId: 0,
      ativo: true,
      dataNascimento: '1990-01-01',
      senha: '',
    });
    this.form.controls.senha.setValidators([Validators.required, Validators.minLength(6)]);
    this.form.controls.senha.updateValueAndValidity();
    this.modalOpen.set(true);
  }

  openEdit(item: User): void {
    this.modalMode.set('edit');
    this.selected.set(item);
    this.form.reset({
      nome: item.nome,
      cpf: item.cpf,
      email: item.email,
      role: item.role,
      departamento: item.departamento,
      cargo: item.cargo,
      empresaSelecionadaId: this.getEmpresaIdByUnidadeId(item.unidadeId ?? 0),
      unidadeId: item.unidadeId ?? 0,
      ativo: item.ativo,
      dataNascimento: toDateInputValue(item.dataNascimento, '1990-01-01'),
      senha: '',
    });
    this.form.controls.senha.clearValidators();
    this.form.controls.senha.updateValueAndValidity();
    this.modalOpen.set(true);
  }

  closeModal(): void {
    if (this.saving()) {
      return;
    }

    this.modalOpen.set(false);
  }

  openCreateUnidade(): void {
    this.unidadeModalMode.set('create');
    this.selectedUnidade.set(null);
    this.unidadeForm.reset({ empresaId: 0, numeroRevenda: 0, revenda: '', cnpj: '', endereco: '' });
    this.unidadeModalOpen.set(true);
  }

  openEditUnidade(item: Unidade): void {
    this.unidadeModalMode.set('edit');
    this.selectedUnidade.set(item);
    this.unidadeForm.reset({
      empresaId: item.empresaId ?? 0,
      numeroRevenda: item.numeroRevenda,
      revenda: item.revenda,
      cnpj: item.cnpj,
      endereco: item.endereco,
    });
    this.unidadeModalOpen.set(true);
  }

  closeUnidadeModal(): void {
    if (!this.saving()) {
      this.unidadeModalOpen.set(false);
    }
  }

  openCreateEmpresa(): void {
    this.empresaModalMode.set('create');
    this.selectedEmpresa.set(null);
    this.empresaForm.reset({ numero: 0, nome: '' });
    this.empresaModalOpen.set(true);
  }

  openEditEmpresa(item: Empresa): void {
    this.empresaModalMode.set('edit');
    this.selectedEmpresa.set(item);
    this.empresaForm.reset({ numero: item.numero, nome: item.nome });
    this.empresaModalOpen.set(true);
  }

  closeEmpresaModal(): void {
    if (!this.saving()) {
      this.empresaModalOpen.set(false);
    }
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      if (this.form.invalid) {
        this.toastr.warning('Confira os campos obrigatorios antes de salvar.', 'Atencao');
      }
      return;
    }

    this.saving.set(true);
    const raw = this.form.getRawValue();
    const unidadeId = raw.unidadeId > 0 ? raw.unidadeId : null;

    if (this.modalMode() === 'create') {
      const payload: UserCreatePayload = {
        nome: raw.nome,
        cpf: raw.cpf,
        email: raw.email,
        role: raw.role as Role,
        departamento: raw.departamento,
        cargo: raw.cargo,
        ativo: raw.ativo,
        unidadeId,
        dataNascimento: raw.dataNascimento,
        senha: raw.senha,
      };

      this.auth.createUser(payload).subscribe({
        next: (created) => {
          this.users.set([created, ...this.users()]);
          this.saving.set(false);
          this.modalOpen.set(false);
          this.toastr.success('Usuario criado com sucesso.', 'Usuarios');
        },
        error: (error) => {
          this.saving.set(false);
          this.toastr.error(this.getErrorMessage('Nao foi possivel criar o usuario.', error), 'Erro');
        },
      });
      return;
    }

    const selected = this.selected();
    if (!selected) {
      this.saving.set(false);
      return;
    }

    const payload: UserUpdatePayload = {
      nome: raw.nome,
      cpf: raw.cpf,
      email: raw.email,
      role: raw.role as Role,
      departamento: raw.departamento,
      cargo: raw.cargo,
      ativo: raw.ativo,
      unidadeId,
      dataNascimento: raw.dataNascimento,
      senha: raw.senha,
    };

    this.auth.updateUser(selected.id, payload).subscribe({
      next: (updated) => {
        this.users.set(this.users().map((item) => item.id === updated.id ? updated : item));
        this.selected.set(updated);
        this.saving.set(false);
        this.modalOpen.set(false);
        this.toastr.success('Usuario atualizado com sucesso.', 'Usuarios');
      },
      error: (error) => {
        this.saving.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel atualizar o usuario.', error), 'Erro');
      },
    });
  }

  submitUnidade(): void {
    if (this.unidadeForm.invalid || this.saving()) {
      this.unidadeForm.markAllAsTouched();
      this.toastr.warning('Preencha empresa, numero da revenda, revenda, CNPJ e endereco.', 'Revendas');
      return;
    }

    this.saving.set(true);
    const payload = this.unidadeForm.getRawValue();
    const selected = this.selectedUnidade();
    const request = selected
      ? this.unidadesService.update(selected.id, payload)
      : this.unidadesService.create(payload);

    request.subscribe({
      next: (saved) => {
        this.unidades.set(selected ? this.unidades().map((item) => item.id === saved.id ? saved : item) : [saved, ...this.unidades()]);
        this.saving.set(false);
        this.unidadeModalOpen.set(false);
        this.toastr.success('Revenda salva com sucesso.', 'Revendas');
      },
      error: (error) => {
        this.saving.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel salvar a revenda.', error), 'Erro');
      },
    });
  }

  submitEmpresa(): void {
    if (this.empresaForm.invalid || this.saving()) {
      this.empresaForm.markAllAsTouched();
      this.toastr.warning('Preencha o numero e o nome da empresa.', 'Empresas');
      return;
    }

    this.saving.set(true);
    const selected = this.selectedEmpresa();
    const payload = this.empresaForm.getRawValue();
    const request = selected
      ? this.unidadesService.updateEmpresa(selected.id, payload)
      : this.unidadesService.createEmpresa(payload);

    request.subscribe({
      next: (empresa) => {
        this.empresasCadastro.set(
          (selected
            ? this.empresasCadastro().map((item) => item.id === empresa.id ? empresa : item)
            : [...this.empresasCadastro(), empresa])
            .sort((a, b) => a.numero - b.numero || a.nome.localeCompare(b.nome)),
        );
        this.saving.set(false);
        this.empresaModalOpen.set(false);
        this.loadUnidades();
        this.toastr.success(selected ? 'Empresa atualizada com sucesso.' : 'Empresa criada com sucesso.', 'Empresas');
      },
      error: (error) => {
        this.saving.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel salvar a empresa.', error), 'Erro');
      },
    });
  }

  onEmpresaUsuarioChange(empresaId: number): void {
    this.form.patchValue({ empresaSelecionadaId: Number(empresaId), unidadeId: 0 });
  }

  setUserSort(field: UserSortField): void {
    this.userSortField.set(field);
    this.userPage.set(1);
  }

  setUnidadeSort(field: UnidadeSortField): void {
    this.unidadeSortField.set(field);
    this.unidadePage.set(1);
  }

  setEmpresaSort(field: EmpresaSortField): void {
    this.empresaSortField.set(field);
    this.empresaPage.set(1);
  }

  toggleSortDirection(): void {
    this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    this.userPage.set(1);
    this.unidadePage.set(1);
    this.empresaPage.set(1);
  }

  previousUserPage(): void {
    this.userPage.set(Math.max(1, this.userPage() - 1));
  }

  nextUserPage(): void {
    this.userPage.set(Math.min(this.totalUserPages(), this.userPage() + 1));
  }

  previousUnidadePage(): void {
    this.unidadePage.set(Math.max(1, this.unidadePage() - 1));
  }

  nextUnidadePage(): void {
    this.unidadePage.set(Math.min(this.totalUnidadePages(), this.unidadePage() + 1));
  }

  previousEmpresaPage(): void {
    this.empresaPage.set(Math.max(1, this.empresaPage() - 1));
  }

  nextEmpresaPage(): void {
    this.empresaPage.set(Math.min(this.totalEmpresaPages(), this.empresaPage() + 1));
  }

  revendasDoUsuario(): Unidade[] {
    const empresaId = Number(this.form.controls.empresaSelecionadaId.value);
    return this.unidades().filter((item) => item.empresaId === empresaId);
  }

  goHome(): void {
    void this.router.navigate(['/hub']);
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

  private getEmpresaIdByUnidadeId(unidadeId: number): number {
    return this.unidades().find((item) => item.id === unidadeId)?.empresaId ?? 0;
  }

  private buildUserTree(users: User[]): UserEmpresaGroup[] {
    const empresas = new Map<string, UserEmpresaGroup>();

    users.forEach((user) => {
      const unidade = this.unidades().find((item) => item.id === user.unidadeId);
      const empresaKey = unidade?.empresaId ? `empresa-${unidade.empresaId}` : 'empresa-sem-vinculo';
      const empresaLabel = unidade ? `${unidade.empresaNumero} - ${unidade.empresa}` : 'Sem empresa vinculada';
      const revendaKey = unidade?.id ? `revenda-${unidade.id}` : 'revenda-sem-vinculo';
      const revendaLabel = unidade ? `${unidade.numeroRevenda} - ${unidade.revenda}` : (user.unidadeNome || 'Sem revenda vinculada');
      const empresa = empresas.get(empresaKey) ?? {
        key: empresaKey,
        label: empresaLabel,
        totalUsuarios: 0,
        totalAtivos: 0,
        revendas: [],
      };
      let revenda = empresa.revendas.find((item) => item.key === revendaKey);

      if (!revenda) {
        revenda = { key: revendaKey, label: revendaLabel, users: [], totalAtivos: 0 };
        empresa.revendas.push(revenda);
      }

      revenda.users.push(user);
      revenda.totalAtivos += user.ativo ? 1 : 0;
      empresa.totalUsuarios += 1;
      empresa.totalAtivos += user.ativo ? 1 : 0;
      empresas.set(empresaKey, empresa);
    });

    return Array.from(empresas.values())
      .map((empresa) => ({
        ...empresa,
        revendas: empresa.revendas
          .map((revenda) => ({
            ...revenda,
            users: this.sortTreeItems(revenda.users, (user) => user.nome),
          }))
          .sort((a, b) => this.compareTreeLabels(a.label, b.label)),
      }))
      .sort((a, b) => this.compareTreeLabels(a.label, b.label));
  }

  private sortTreeItems<T>(items: T[], labelSelector: (item: T) => string): T[] {
    const direction = this.sortDirection() === 'asc' ? 1 : -1;
    return items.slice().sort((a, b) => this.normalize(labelSelector(a)).localeCompare(this.normalize(labelSelector(b))) * direction);
  }

  private compareTreeLabels(a: string, b: string): number {
    const direction = this.sortDirection() === 'asc' ? 1 : -1;
    const aEmpty = a.startsWith('Sem ');
    const bEmpty = b.startsWith('Sem ');

    if (aEmpty !== bEmpty) {
      return aEmpty ? 1 : -1;
    }

    return this.normalize(a).localeCompare(this.normalize(b)) * direction;
  }

  private normalize(value: string | number | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  private sortItems<T>(items: T[], field: keyof T): T[] {
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

  private totalPages(count: number): number {
    return Math.max(1, Math.ceil(count / this.pageSize()));
  }

  private pageItems<T>(items: T[], page: number): T[] {
    const safePage = Math.min(Math.max(page, 1), this.totalPages(items.length));
    return items.slice((safePage - 1) * this.pageSize(), safePage * this.pageSize());
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
