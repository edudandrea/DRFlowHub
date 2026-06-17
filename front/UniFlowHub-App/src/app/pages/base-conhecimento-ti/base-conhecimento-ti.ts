import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, HostListener, OnInit, PLATFORM_ID, computed, inject, signal, ViewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { BaseConhecimentoTIService } from '../../core/base-conhecimento-ti.service';
import { BaseConhecimentoTI, BaseConhecimentoTIPayload } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';

@Component({
  selector: 'app-base-conhecimento-ti',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './base-conhecimento-ti.html',
  styleUrl: './base-conhecimento-ti.scss',
})
export class BaseConhecimentoTIPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly service = inject(BaseConhecimentoTIService);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly itens = signal<BaseConhecimentoTI[]>([]);
  readonly selected = signal<BaseConhecimentoTI | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly modalOpen = signal(false);
  readonly search = signal('');
  readonly selectedFileName = signal('');
  readonly profileMenuOpen = signal(false);
  readonly totalComAnexo = computed(() => this.itens().filter((item) => !!item.arquivoUrl).length);
  readonly categorias = computed(() => new Set(this.itens().map((item) => item.categoria).filter(Boolean)).size);
  readonly filtered = computed(() => {
    const term = this.normalize(this.search());
    return this.itens().filter((item) => !term || [
      item.titulo,
      item.categoria,
      item.descricao,
      item.tags,
      item.arquivoNome,
      item.autorNome,
    ].some((value) => this.normalize(value).includes(term)));
  });

  private selectedFile: File | null = null;
  @ViewChild('documentoInput') private documentoInput?: ElementRef<HTMLInputElement>;

  readonly form = this.fb.nonNullable.group({
    titulo: ['', Validators.required],
    categoria: ['Geral', Validators.required],
    descricao: ['', Validators.required],
    tags: [''],
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    this.load();
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.service.list().subscribe({
      next: (items) => {
        this.itens.set(items);
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar a base de conhecimento.', 'TI');
      },
    });
  }

  openNew(): void {
    this.selected.set(null);
    this.form.reset({ titulo: '', categoria: 'Geral', descricao: '', tags: '' });
    this.clearFile();
    this.modalOpen.set(true);
  }

  select(item: BaseConhecimentoTI): void {
    this.selected.set(item);
    this.form.reset({
      titulo: item.titulo,
      categoria: item.categoria || 'Geral',
      descricao: item.descricao,
      tags: item.tags,
    });
    this.clearFile();
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.selected.set(null);
    this.clearFile();
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
      this.toastr.warning('Preencha título e descrição.', 'Atenção');
      return;
    }

    this.saving.set(true);
    const payload = this.form.getRawValue() as BaseConhecimentoTIPayload;
    const selected = this.selected();
    const request = selected
      ? this.service.update(selected.id, payload, this.selectedFile)
      : this.service.create(payload, this.selectedFile);

    request.subscribe({
      next: (saved) => {
        this.itens.set(selected ? this.itens().map((item) => item.id === saved.id ? saved : item) : [saved, ...this.itens()]);
        this.saving.set(false);
        this.modalOpen.set(false);
        this.selected.set(null);
        this.clearFile();
        this.toastr.success('Conhecimento salvo.', 'TI');
      },
      error: () => {
        this.saving.set(false);
        this.toastr.error('Não foi possível salvar o conhecimento.', 'Erro');
      },
    });
  }

  deleteSelected(): void {
    const selected = this.selected();
    if (!selected || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.service.delete(selected.id).subscribe({
      next: () => {
        this.itens.set(this.itens().filter((item) => item.id !== selected.id));
        this.saving.set(false);
        this.modalOpen.set(false);
        this.selected.set(null);
        this.toastr.success('Conhecimento removido.', 'TI');
      },
      error: () => {
        this.saving.set(false);
        this.toastr.error('Não foi possível remover o conhecimento.', 'Erro');
      },
    });
  }

  downloadDocument(item = this.selected()): void {
    if (!item?.arquivoUrl) {
      this.toastr.info('Este conhecimento não possui documento.', 'Documento');
      return;
    }

    this.service.downloadDocument(item.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = item.arquivoNome || 'documento';
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

  private clearFile(): void {
    this.selectedFile = null;
    this.selectedFileName.set('');
    if (this.documentoInput?.nativeElement) {
      this.documentoInput.nativeElement.value = '';
    }
  }

  private normalize(value: string | number | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }
}
