import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, HostListener, OnInit, PLATFORM_ID, computed, inject, signal, ViewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import QRCode from 'qrcode';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { EquipamentosTIService } from '../../core/equipamentos-ti.service';
import { EquipamentoTI, EquipamentoTIPayload, Unidade, User } from '../../core/models';
import { ThemeService } from '../../core/theme.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { UnidadesService } from '../../core/unidades.service';

@Component({
  selector: 'app-equipamentos-ti',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './equipamentos-ti.html',
  styleUrl: './equipamentos-ti.scss',
})
export class EquipamentosTIPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly service = inject(EquipamentosTIService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly itens = signal<EquipamentoTI[]>([]);
  readonly usuarios = signal<User[]>([]);
  readonly unidades = signal<Unidade[]>([]);
  readonly selected = signal<EquipamentoTI | null>(null);
  readonly qrCodeUrl = signal('');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly modalOpen = signal(false);
  readonly editing = signal(false);
  readonly qrNeedsRefresh = signal(false);
  readonly deleteOpen = signal(false);
  readonly deleting = signal(false);
  readonly search = signal('');
  readonly selectedFileName = signal('');
  readonly profileMenuOpen = signal(false);
  readonly isInventoryRoute = computed(() => this.router.url.includes('/inventario'));
  readonly totalInventario = computed(() => this.itens().filter((item) => this.isInventoryItem(item)).length);
  readonly emTransito = computed(() => this.itens().filter((item) => item.status === 'Enviado' || item.status === 'Em transito').length);
  readonly recebidos = computed(() => this.itens().filter((item) => item.status === 'Recebido').length);
  readonly atrasados = computed(() => this.itens().filter((item) => {
    if (!item.dataPrevistaRetorno || item.status === 'Recebido') {
      return false;
    }
    return new Date(item.dataPrevistaRetorno).getTime() < Date.now();
  }).length);
  readonly filtered = computed(() => {
    const term = this.normalize(this.search());
    return this.itens().filter((item) => !term || [
      item.tipo,
      item.patrimonio,
      item.modelo,
      item.serial,
      item.responsavel,
      item.status,
      item.destino,
      item.filialCompra,
      item.notaFiscalCompra,
      item.usuarioResponsavelNome,
      item.usuarioResponsavelEmail,
    ].some((value) => this.normalize(value).includes(term)));
  });

  private selectedFile: File | null = null;
  @ViewChild('documentoInput') private documentoInput?: ElementRef<HTMLInputElement>;

  readonly form = this.fb.nonNullable.group({
    tipo: ['Notebook', Validators.required],
    patrimonio: ['', Validators.required],
    modelo: [''],
    serial: [''],
    status: ['Enviado', Validators.required],
    origem: ['TI', Validators.required],
    destino: ['', Validators.required],
    responsavel: ['', Validators.required],
    filialCompraId: [null as number | null, Validators.required],
    filialCompra: ['', Validators.required],
    notaFiscalCompra: ['', Validators.required],
    usuarioResponsavelId: [null as number | null, Validators.required],
    usuarioResponsavelNome: ['', Validators.required],
    usuarioResponsavelEmail: [''],
    usuarioResponsavelDepartamento: [''],
    usuarioResponsavelUnidade: [''],
    dataPrevistaRetorno: [''],
    observacoes: [''],
  });

  readonly deleteForm = this.fb.nonNullable.group({
    motivo: ['', Validators.required],
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }
    this.loadUnidades();
    this.loadUsuarios();
    this.load();
    this.form.valueChanges.subscribe(() => {
      if (!this.editing() || !this.selected()) {
        return;
      }

      this.qrCodeUrl.set('');
      this.qrNeedsRefresh.set(true);
    });
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.service.list().subscribe({
      next: (items) => {
        this.itens.set(items);
        this.selected.set(null);
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar os equipamentos.', 'TI');
      },
    });
  }

  select(item: EquipamentoTI): void {
    this.selected.set(item);
    this.editing.set(false);
    this.qrNeedsRefresh.set(false);
    this.patchForm(item);
    this.modalOpen.set(true);
  }

  openNewMovement(): void {
    this.selected.set(null);
    this.editing.set(true);
    this.qrNeedsRefresh.set(false);
    this.qrCodeUrl.set('');
    this.form.reset({
      tipo: 'Notebook',
      patrimonio: '',
      modelo: '',
      serial: '',
      status: this.isInventoryRoute() ? 'Em uso' : 'Enviado',
      origem: 'TI',
      destino: '',
      responsavel: '',
      filialCompraId: null,
      filialCompra: '',
      notaFiscalCompra: '',
      usuarioResponsavelId: null,
      usuarioResponsavelNome: '',
      usuarioResponsavelEmail: '',
      usuarioResponsavelDepartamento: '',
      usuarioResponsavelUnidade: '',
      dataPrevistaRetorno: '',
      observacoes: '',
    });
    this.selectedFile = null;
    this.selectedFileName.set('');
    if (this.documentoInput?.nativeElement) {
      this.documentoInput.nativeElement.value = '';
    }
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.selected.set(null);
    this.editing.set(false);
    this.qrNeedsRefresh.set(false);
    this.deleteOpen.set(false);
    this.qrCodeUrl.set('');
    this.deleteForm.reset({ motivo: '' });
    this.selectedFile = null;
    this.selectedFileName.set('');
    if (this.documentoInput?.nativeElement) {
      this.documentoInput.nativeElement.value = '';
    }
  }

  openDeleteConfirmation(): void {
    if (!this.selected()) {
      return;
    }

    this.deleteForm.reset({ motivo: '' });
    this.deleteOpen.set(true);
  }

  cancelDelete(): void {
    if (this.deleting()) {
      return;
    }

    this.deleteOpen.set(false);
    this.deleteForm.reset({ motivo: '' });
  }

  confirmDelete(): void {
    const selected = this.selected();
    if (!selected || this.deleting()) {
      return;
    }

    if (this.deleteForm.invalid) {
      this.deleteForm.markAllAsTouched();
      this.toastr.warning('Informe o motivo da exclusao.', 'Atencao');
      return;
    }

    this.deleting.set(true);
    this.service.delete(selected.id, this.deleteForm.controls.motivo.value).subscribe({
      next: () => {
        this.itens.set(this.itens().filter((item) => item.id !== selected.id));
        this.deleting.set(false);
        this.toastr.success('Inventario excluido.', 'TI');
        this.closeModal();
      },
      error: (error) => {
        this.deleting.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel excluir o inventario.', error), 'Erro');
      },
    });
  }

  editInventory(): void {
    if (!this.selected()) {
      return;
    }

    this.editing.set(true);
    this.qrNeedsRefresh.set(false);
  }

  loadUnidades(): void {
    this.unidadesService.list().subscribe({
      next: (unidades) => this.unidades.set(unidades),
      error: () => this.toastr.error('Nao foi possivel carregar as filiais.', 'Erro'),
    });
  }

  loadUsuarios(): void {
    this.auth.listUsers().subscribe({
      next: (usuarios) => this.usuarios.set(usuarios.filter((usuario) => usuario.ativo)),
      error: () => this.toastr.error('Nao foi possivel carregar os usuarios.', 'Erro'),
    });
  }

  onFilialChange(value: string): void {
    const filialId = Number(value) || null;
    const filial = this.unidades().find((item) => item.id === filialId) ?? null;
    this.form.patchValue({
      filialCompraId: filial?.id ?? null,
      filialCompra: filial?.nome ?? '',
      origem: filial?.nome ?? this.form.controls.origem.value,
    });
  }

  onUsuarioChange(value: string): void {
    const usuarioId = Number(value) || null;
    const usuario = this.usuarios().find((item) => item.id === usuarioId) ?? null;
    this.form.patchValue({
      usuarioResponsavelId: usuario?.id ?? null,
      usuarioResponsavelNome: usuario?.nome ?? '',
      usuarioResponsavelEmail: usuario?.email ?? '',
      usuarioResponsavelDepartamento: usuario?.departamento ?? '',
      usuarioResponsavelUnidade: usuario?.unidadeNome ?? '',
      responsavel: usuario?.nome ?? '',
      destino: usuario?.unidadeNome ?? '',
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedFile = file;
    this.selectedFileName.set(file?.name ?? '');
  }

  submit(): void {
    if (!this.editing()) {
      return;
    }

    this.syncInventoryLookups();

    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      this.toastr.warning('Preencha os campos obrigatórios.', 'Atenção');
      return;
    }

    this.saving.set(true);
    const payload = this.form.getRawValue() as EquipamentoTIPayload;
    const selected = this.selected();
    const request = selected
      ? this.service.update(selected.id, payload)
      : this.service.create(payload, this.selectedFile);

    request.subscribe({
      next: (saved) => {
        this.itens.set(selected ? this.itens().map((item) => item.id === saved.id ? saved : item) : [saved, ...this.itens()]);
        this.selected.set(saved);
        this.editing.set(false);
        if (selected) {
          this.qrCodeUrl.set('');
          this.qrNeedsRefresh.set(true);
        } else {
          this.qrNeedsRefresh.set(false);
          void this.generateQrCode(saved);
        }
        this.saving.set(false);
        this.toastr.success('Controle de equipamento salvo.', 'TI');
      },
      error: (error) => {
        this.saving.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel salvar o equipamento.', error), 'Detalhe');
        this.toastr.error('Não foi possível salvar o equipamento.', 'Erro');
      },
    });
  }

  async generateQrCode(item = this.selected()): Promise<void> {
    const source = this.editing() && this.selected() ? this.buildQrItemFromForm(this.selected()!) : item;
    if (!source) {
      this.qrCodeUrl.set('');
      return;
    }

    const url = this.buildQrTicketUrl(source);
    this.qrCodeUrl.set(await QRCode.toDataURL(url, { width: 256, margin: 1 }));
    this.qrNeedsRefresh.set(false);
  }

  printQrCode(item = this.selected()): void {
    const qr = this.qrCodeUrl();
    if (!item || !qr) {
      this.toastr.info('Gere o QR Code antes de imprimir.', 'Inventario');
      return;
    }

    const win = window.open('', '_blank', 'width=420,height=620');
    if (!win) {
      this.toastr.warning('O navegador bloqueou a janela de impressao.', 'Inventario');
      return;
    }

    win.document.write(this.buildQrPrintHtml(item, qr));
    win.document.close();
    win.focus();
    win.print();
  }

  downloadDocument(item = this.selected()): void {
    if (!item?.documentoUrl) {
      this.toastr.info('Este registro não possui documento.', 'Documento');
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

  private patchForm(item: EquipamentoTI): void {
    this.form.reset({
      tipo: item.tipo,
      patrimonio: item.patrimonio,
      modelo: item.modelo,
      serial: item.serial,
      status: item.status,
      origem: item.origem,
      destino: item.destino,
      responsavel: item.responsavel,
      filialCompraId: item.filialCompraId ?? null,
      filialCompra: item.filialCompra,
      notaFiscalCompra: item.notaFiscalCompra,
      usuarioResponsavelId: item.usuarioResponsavelId ?? null,
      usuarioResponsavelNome: item.usuarioResponsavelNome || item.responsavel,
      usuarioResponsavelEmail: item.usuarioResponsavelEmail,
      usuarioResponsavelDepartamento: item.usuarioResponsavelDepartamento,
      usuarioResponsavelUnidade: item.usuarioResponsavelUnidade || item.destino,
      dataPrevistaRetorno: item.dataPrevistaRetorno ? item.dataPrevistaRetorno.slice(0, 10) : '',
      observacoes: item.observacoes,
    });
    void this.generateQrCode(item);
  }

  private syncInventoryLookups(): void {
    const filialId = Number(this.form.controls.filialCompraId.value) || null;
    const usuarioId = Number(this.form.controls.usuarioResponsavelId.value) || null;
    const filial = this.unidades().find((item) => item.id === filialId) ?? null;
    const usuario = this.usuarios().find((item) => item.id === usuarioId) ?? null;

    this.form.patchValue({
      filialCompraId: filial?.id ?? filialId,
      filialCompra: filial?.nome || this.form.controls.filialCompra.value,
      origem: filial?.nome || this.form.controls.origem.value || 'TI',
      usuarioResponsavelId: usuario?.id ?? usuarioId,
      usuarioResponsavelNome: usuario?.nome || this.form.controls.usuarioResponsavelNome.value || this.form.controls.responsavel.value,
      usuarioResponsavelEmail: usuario?.email || this.form.controls.usuarioResponsavelEmail.value,
      usuarioResponsavelDepartamento: usuario?.departamento || this.form.controls.usuarioResponsavelDepartamento.value,
      usuarioResponsavelUnidade: usuario?.unidadeNome || this.form.controls.usuarioResponsavelUnidade.value || this.form.controls.destino.value,
      responsavel: usuario?.nome || this.form.controls.responsavel.value || this.form.controls.usuarioResponsavelNome.value,
      destino: usuario?.unidadeNome || this.form.controls.destino.value || this.form.controls.usuarioResponsavelUnidade.value,
    });
  }

  private isInventoryItem(item: EquipamentoTI): boolean {
    return !!(item.filialCompra || item.notaFiscalCompra || item.usuarioResponsavelNome);
  }

  private buildQrItemFromForm(base: EquipamentoTI): EquipamentoTI {
    const value = this.form.getRawValue();
    return {
      ...base,
      tipo: value.tipo,
      patrimonio: value.patrimonio,
      modelo: value.modelo,
      serial: value.serial,
      status: value.status,
      origem: value.origem,
      destino: value.destino,
      responsavel: value.responsavel,
      filialCompraId: value.filialCompraId,
      filialCompra: value.filialCompra,
      notaFiscalCompra: value.notaFiscalCompra,
      usuarioResponsavelId: value.usuarioResponsavelId,
      usuarioResponsavelNome: value.usuarioResponsavelNome,
      usuarioResponsavelEmail: value.usuarioResponsavelEmail,
      usuarioResponsavelDepartamento: value.usuarioResponsavelDepartamento,
      usuarioResponsavelUnidade: value.usuarioResponsavelUnidade,
      dataPrevistaRetorno: value.dataPrevistaRetorno || null,
      observacoes: value.observacoes,
    };
  }

  private buildQrTicketUrl(item: EquipamentoTI): string {
    const params = new URLSearchParams({
      abrirChamado: '1',
      titulo: `Chamado - ${item.tipo} ${item.patrimonio}`.trim(),
      categoria: 'Equipamento',
      solicitante: item.usuarioResponsavelNome || item.responsavel,
      unidade: item.usuarioResponsavelUnidade || item.destino || item.filialCompra,
      departamento: item.usuarioResponsavelDepartamento || '',
      equipamentoNome: this.equipmentLabel(item),
      equipamentoSistemaOperacional: item.tipo,
      observacoes: [
        `Patrimonio: ${item.patrimonio}`,
        `Tipo: ${item.tipo}`,
        item.modelo ? `Modelo: ${item.modelo}` : '',
        item.serial ? `Serial: ${item.serial}` : '',
        item.filialCompra ? `Filial de compra: ${item.filialCompra}` : '',
        item.notaFiscalCompra ? `Nota fiscal: ${item.notaFiscalCompra}` : '',
        item.usuarioResponsavelNome ? `Usuario responsavel: ${item.usuarioResponsavelNome}` : '',
      ].filter(Boolean).join('\n'),
    });

    return `${window.location.origin}/ti?${params.toString()}`;
  }

  private equipmentLabel(item: EquipamentoTI): string {
    return [item.tipo, item.patrimonio, item.modelo, item.serial].filter(Boolean).join(' - ');
  }

  private buildQrPrintHtml(item: EquipamentoTI, qr: string): string {
    return `<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8" />
  <title>QR Code - ${this.escapeHtml(item.patrimonio)}</title>
  <style>
    @page { size: 90mm 70mm; margin: 6mm; }
    * { box-sizing: border-box; }
    body { margin: 0; color: #111827; font-family: Arial, Helvetica, sans-serif; }
    .label { display: grid; place-items: center; gap: 6px; text-align: center; }
    img { width: 42mm; height: 42mm; }
    strong { font-size: 13px; }
    span { display: block; font-size: 10px; }
  </style>
</head>
<body>
  <section class="label">
    <strong>UniFlowHub - Abertura de chamado</strong>
    <img src="${qr}" alt="QR Code do equipamento" />
    <span>${this.escapeHtml(this.equipmentLabel(item))}</span>
    <span>NF: ${this.escapeHtml(item.notaFiscalCompra || '-')}</span>
  </section>
</body>
</html>`;
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
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
        return Object.values(error.error.errors).flat().join(' ');
      }
    }

    return fallback;
  }

  private normalize(value: string | number | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }
}
