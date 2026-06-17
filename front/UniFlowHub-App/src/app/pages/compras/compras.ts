import { CurrencyPipe, DatePipe, isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, HostListener, OnDestroy, OnInit, PLATFORM_ID, computed, inject, signal, ViewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { ComprasService } from '../../core/compras.service';
import { SolicitacaoCompra, SolicitacaoCompraComunicação, SolicitacaoCompraPayload, SolicitacaoCompraUpdatePayload, Unidade } from '../../core/models';
import { ThemeService } from '../../core/theme.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { UnidadesService } from '../../core/unidades.service';

type CompraTab = 'minhas' | 'aprovacao' | 'compras' | 'todas';
type CompraSortField = 'id' | 'titulo' | 'solicitante' | 'unidade' | 'departamento' | 'valorEstimado' | 'status' | 'dataSolicitacao';

@Component({
  selector: 'app-compras',
  imports: [ReactiveFormsModule, DatePipe, CurrencyPipe],
  templateUrl: './compras.html',
  styleUrl: './compras.scss',
})
export class ComprasPage implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly service = inject(ComprasService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly items = signal<SolicitacaoCompra[]>([]);
  readonly unidades = signal<Unidade[]>([]);
  readonly selected = signal<SolicitacaoCompra | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly approving = signal(false);
  readonly updating = signal(false);
  readonly modalOpen = signal(false);
  readonly createModalOpen = signal(false);
  readonly detailTab = signal<'detalhes' | 'comunicacao'>('detalhes');
  readonly comunicacoes = signal<SolicitacaoCompraComunicação[]>([]);
  readonly loadingComunicacoes = signal(false);
  readonly sendingMessage = signal(false);
  readonly activeTab = signal<CompraTab>('minhas');
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly sortField = signal<CompraSortField>('id');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');
  readonly selectedFileName = signal('');
  readonly profileMenuOpen = signal(false);
  readonly canApprove = computed(() => this.auth.hasAnyRole(['Admin', 'Diretoria']));
  readonly canBuy = computed(() => this.auth.hasAccess('compras-admin'));
  readonly aguardandoDiretoria = computed(() => this.items().filter((item) => item.status === 'Aguardando Diretoria').length);
  readonly emCompras = computed(() => this.items().filter((item) => item.status.includes('Compras') || item.status === 'Em compras').length);
  readonly concluidas = computed(() => this.items().filter((item) => item.status === 'Concluida').length);
  readonly visibleItems = computed(() => {
    const currentId = this.user()?.id;
    let items = this.items();

    if (this.activeTab() === 'minhas') {
      items = items.filter((item) => item.userid === currentId);
    } else if (this.activeTab() === 'aprovacao') {
      items = items.filter((item) => item.status === 'Aguardando Diretoria');
    } else if (this.activeTab() === 'compras') {
      items = items.filter((item) => item.status === 'Aprovada - Enviada para Compras' || item.status === 'Em compras');
    }

    const term = this.normalize(this.search());
    const filtered = items.filter((item) => !term || [
      item.titulo,
      item.solicitante,
      item.departamento,
      item.categoria,
      item.status,
      item.comprador,
    ].some((value) => this.normalize(value).includes(term)));

    return this.sortItems(filtered);
  });
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.visibleItems().length / this.pageSize())));
  readonly pagedItems = computed(() => this.visibleItems().slice((this.safePage() - 1) * this.pageSize(), this.safePage() * this.pageSize()));

  private selectedFile: File | null = null;
  private communicationRefreshId: ReturnType<typeof setInterval> | null = null;
  @ViewChild('documentoInput') private documentoInput?: ElementRef<HTMLInputElement>;

  readonly form = this.fb.nonNullable.group({
    titulo: ['', Validators.required],
    categoria: ['Material', Validators.required],
    descricao: ['', Validators.required],
    solicitante: ['', Validators.required],
    unidade: ['', Validators.required],
    departamento: ['', Validators.required],
    valorEstimado: [0, [Validators.required, Validators.min(0)]],
    fornecedorSugerido: ['', Validators.required],
    prioridade: ['Media', Validators.required],
    justificativa: ['', Validators.required],
    observacoes: [''],
  });

  readonly approvalForm = this.fb.nonNullable.group({
    observacoesAprovacao: [''],
  });

  readonly buyerForm = this.fb.nonNullable.group({
    titulo: ['', Validators.required],
    categoria: ['', Validators.required],
    descricao: ['', Validators.required],
    solicitante: ['', Validators.required],
    unidade: ['', Validators.required],
    departamento: ['', Validators.required],
    valorEstimado: [0, [Validators.required, Validators.min(0)]],
    fornecedorSugerido: ['', Validators.required],
    prioridade: ['', Validators.required],
    justificativa: ['', Validators.required],
    observacoes: [''],
    status: ['', Validators.required],
    comprador: [''],
    observacoesCompras: [''],
  });

  readonly messageForm = this.fb.nonNullable.group({
    mensagem: ['', Validators.required],
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    const user = this.user();
    if (user) {
      this.form.patchValue({ solicitante: user.nome, unidade: this.getUserUnidadeName(), departamento: user.departamento });
    }
    if (this.canApprove()) {
      this.activeTab.set('aprovacao');
    } else if (this.canBuy()) {
      this.activeTab.set('compras');
    }
    this.loadUnidades();
    this.load();
  }

  ngOnDestroy(): void {
    this.stopCommunicationRefresh();
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
        this.items.set(items);
        this.selected.set(null);
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar as solicitações de compras.', 'Compras');
      },
    });
  }

  setTab(tab: CompraTab): void {
    this.activeTab.set(tab);
    this.page.set(1);
    this.closeModal();
  }

  setSort(field: CompraSortField): void {
    if (this.sortField() === field) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortField.set(field);
      this.sortDirection.set(field === 'id' || field === 'dataSolicitacao' ? 'desc' : 'asc');
    }
    this.page.set(1);
  }

  previousPage(): void {
    this.page.set(Math.max(1, this.safePage() - 1));
  }

  nextPage(): void {
    this.page.set(Math.min(this.totalPages(), this.safePage() + 1));
  }

  openCreateModal(): void {
    this.createModalOpen.set(true);
  }

  closeCreateModal(): void {
    if (!this.saving()) {
      this.createModalOpen.set(false);
    }
  }

  select(item: SolicitacaoCompra): void {
    this.selected.set(item);
    this.detailTab.set('detalhes');
    this.patchBuyerForm(item);
    this.approvalForm.reset({ observacoesAprovacao: item.observacoesAprovacao || '' });
    this.messageForm.reset({ mensagem: '' });
    this.loadComunicacoes(item.id);
    this.modalOpen.set(true);
    this.startCommunicationRefresh();
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.stopCommunicationRefresh();
    this.selected.set(null);
    this.comunicacoes.set([]);
    this.approvalForm.reset({ observacoesAprovacao: '' });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedFile = file;
    this.selectedFileName.set(file?.name ?? '');
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      this.toastr.warning('Preencha os campos obrigatórios.', 'Atenção');
      return;
    }

    this.saving.set(true);
    const payload: SolicitacaoCompraPayload = {
      ...this.form.getRawValue(),
      userid: this.user()?.id ?? 0,
    };

    this.service.create(payload, this.selectedFile).subscribe({
      next: (created) => {
        this.items.set([created, ...this.items()]);
        this.selected.set(null);
        this.form.patchValue({
          titulo: '',
          descricao: '',
          unidade: this.getUserUnidadeName(),
          valorEstimado: 0,
          fornecedorSugerido: '',
          prioridade: 'Media',
          justificativa: '',
          observacoes: '',
        });
        this.selectedFile = null;
        this.selectedFileName.set('');
        if (this.documentoInput?.nativeElement) {
          this.documentoInput.nativeElement.value = '';
        }
        this.saving.set(false);
        this.closeCreateModal();
        this.toastr.success('Solicitação enviada para aprovação da Diretoria.', 'Compras');
      },
      error: (error) => {
        this.saving.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível criar a solicitação.', error), 'Erro');
      },
    });
  }

  approve(aprovada: boolean): void {
    const selected = this.selected();
    if (!selected || this.approving()) {
      return;
    }

    this.approving.set(true);
    this.service.approve(selected.id, aprovada, this.approvalForm.controls.observacoesAprovacao.value).subscribe({
      next: (updated) => {
        this.items.set(this.items().map((item) => item.id === updated.id ? updated : item));
        this.selected.set(updated);
        this.patchBuyerForm(updated);
        this.approving.set(false);
        this.toastr.success(aprovada ? 'Solicitacao aprovada e enviada para Compras.' : 'Solicitacao reprovada.', 'Diretoria');
      },
      error: () => {
        this.approving.set(false);
        this.toastr.error('Não foi possível registrar a aprovação.', 'Erro');
      },
    });
  }

  sendMessage(): void {
    const selected = this.selected();
    if (!selected || this.isFinalized(selected) || this.messageForm.invalid || this.sendingMessage()) {
      this.messageForm.markAllAsTouched();
      if (this.isFinalized(selected)) {
        this.toastr.info('Solicitações concluídas ou canceladas não permitem novas mensagens.', 'Compras');
      } else if (this.messageForm.invalid) {
        this.toastr.warning('Escreva uma mensagem antes de enviar.', 'Atenção');
      }
      return;
    }

    this.sendingMessage.set(true);
    this.service.sendComunicação(selected.id, this.messageForm.controls.mensagem.value).subscribe({
      next: (message) => {
        this.comunicacoes.set([...this.comunicacoes(), message]);
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

  updateBuyer(): void {
    const selected = this.selected();
    if (!selected || this.buyerForm.invalid || this.updating()) {
      this.buyerForm.markAllAsTouched();
      this.toastr.warning('Confira os campos antes de salvar.', 'Atenção');
      return;
    }

    this.updating.set(true);
    this.service.update(selected.id, this.buyerForm.getRawValue() as SolicitacaoCompraUpdatePayload).subscribe({
      next: (updated) => {
        this.items.set(this.items().map((item) => item.id === updated.id ? updated : item));
        this.selected.set(updated);
        this.patchBuyerForm(updated);
        this.updating.set(false);
        this.toastr.success('Etapa de compras atualizada.', 'Compras');
      },
      error: () => {
        this.updating.set(false);
        this.toastr.error('Não foi possível atualizar a solicitação.', 'Erro');
      },
    });
  }

  downloadDocument(item = this.selected()): void {
    if (!item?.documentoUrl) {
      this.toastr.info('Esta solicitação não possui documento.', 'Documento');
      return;
    }

    this.service.downloadDocument(item.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = item.documentoUrl.split('/').pop() || 'documento';
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toastr.error('Não foi possível baixar o documento.', 'Erro'),
    });
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

  private patchBuyerForm(item: SolicitacaoCompra): void {
    this.buyerForm.reset({
      titulo: item.titulo,
      categoria: item.categoria,
      descricao: item.descricao,
      solicitante: item.solicitante,
      unidade: item.unidade,
      departamento: item.departamento,
      valorEstimado: item.valorEstimado,
      fornecedorSugerido: item.fornecedorSugerido,
      prioridade: item.prioridade,
      justificativa: item.justificativa,
      observacoes: item.observacoes,
      status: item.status,
      comprador: item.comprador || this.user()?.nome || '',
      observacoesCompras: item.observacoesCompras,
    });
  }

  isFinalized(item: SolicitacaoCompra | null): boolean {
    return !!item && (!!item.dataConclusao || item.status === 'Concluida' || item.status === 'Cancelada' || item.status === 'Reprovada');
  }

  private loadComunicacoes(id: number): void {
    this.loadingComunicacoes.set(true);
    this.service.listComunicacoes(id).subscribe({
      next: (items) => {
        this.comunicacoes.set(items);
        this.loadingComunicacoes.set(false);
      },
      error: (error) => {
        this.comunicacoes.set([]);
        this.loadingComunicacoes.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível carregar a comunicação.', error), 'Erro');
      },
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
    if (!selected || !this.modalOpen()) {
      return;
    }

    this.service.listComunicacoes(selected.id).subscribe({
      next: (items) => {
        if (this.selected()?.id === selected.id && this.modalOpen()) {
          this.comunicacoes.set(items);
        }
      },
      error: () => undefined,
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

  private sortItems(items: SolicitacaoCompra[]): SolicitacaoCompra[] {
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

  private getErrorMessage(fallback: string, error?: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error;
      }

      if (error.error?.title) {
        return error.error.title;
      }

      if (error.error?.errors) {
        const messages = Object.values(error.error.errors).flat();
        if (messages.length > 0) {
          return messages.join(' ');
        }
      }
    }

    return fallback;
  }

  private getUserUnidadeName(): string {
    const user = this.user();
    if (!user) {
      return '';
    }

    return user.unidadeNome || this.unidades().find((unidade) => unidade.id === user.unidadeId)?.nome || '';
  }
}
