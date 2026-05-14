import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { Empresa, Unidade } from '../../core/models';
import { UnidadesService } from '../../core/unidades.service';

@Component({
  selector: 'app-empresas-revendas',
  imports: [ReactiveFormsModule],
  templateUrl: './empresas-revendas.html',
  styleUrl: './empresas-revendas.scss',
})
export class EmpresasRevendasPage implements OnInit {
  private readonly service = inject(UnidadesService);
  private readonly fb = inject(FormBuilder);
  private readonly toastr = inject(ToastrService);

  readonly empresas = signal<Empresa[]>([]);
  readonly revendas = signal<Unidade[]>([]);
  readonly selectedEmpresa = signal<Empresa | null>(null);
  readonly selectedRevenda = signal<Unidade | null>(null);
  readonly empresaModalOpen = signal(false);
  readonly revendaModalOpen = signal(false);
  readonly saving = signal(false);

  readonly empresaForm = this.fb.nonNullable.group({
    numero: [0, [Validators.required, Validators.min(1)]],
    nome: ['', Validators.required],
  });

  readonly revendaForm = this.fb.nonNullable.group({
    empresaId: [0, [Validators.required, Validators.min(1)]],
    numeroRevenda: [0, [Validators.required, Validators.min(1)]],
    revenda: ['', Validators.required],
    cnpj: ['', Validators.required],
    endereco: ['', Validators.required],
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.listEmpresas().subscribe({ next: (items) => this.empresas.set(items.sort((a, b) => a.numero - b.numero)) });
    this.service.list().subscribe({ next: (items) => this.revendas.set(items) });
  }

  editEmpresa(item: Empresa): void {
    this.selectedEmpresa.set(item);
    this.empresaForm.reset({ numero: item.numero, nome: item.nome });
    this.empresaModalOpen.set(true);
  }

  novaEmpresa(): void {
    this.selectedEmpresa.set(null);
    this.empresaForm.reset({ numero: 0, nome: '' });
    this.empresaModalOpen.set(true);
  }

  closeEmpresaModal(): void {
    if (!this.saving()) {
      this.empresaModalOpen.set(false);
    }
  }

  saveEmpresa(): void {
    if (this.empresaForm.invalid) {
      this.empresaForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const selected = this.selectedEmpresa();
    const request = selected ? this.service.updateEmpresa(selected.id, this.empresaForm.getRawValue()) : this.service.createEmpresa(this.empresaForm.getRawValue());
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.empresaModalOpen.set(false);
        this.selectedEmpresa.set(null);
        this.load();
        this.toastr.success('Empresa salva.', 'Cadastros');
      },
      error: () => {
        this.saving.set(false);
        this.toastr.error('Nao foi possivel salvar a empresa.', 'Erro');
      },
    });
  }

  editRevenda(item: Unidade): void {
    this.selectedRevenda.set(item);
    this.revendaForm.reset({
      empresaId: item.empresaId ?? 0,
      numeroRevenda: item.numeroRevenda,
      revenda: item.revenda,
      cnpj: item.cnpj,
      endereco: item.endereco,
    });
    this.revendaModalOpen.set(true);
  }

  novaRevenda(): void {
    this.selectedRevenda.set(null);
    this.revendaForm.reset({ empresaId: 0, numeroRevenda: 0, revenda: '', cnpj: '', endereco: '' });
    this.revendaModalOpen.set(true);
  }

  closeRevendaModal(): void {
    if (!this.saving()) {
      this.revendaModalOpen.set(false);
    }
  }

  saveRevenda(): void {
    if (this.revendaForm.invalid) {
      this.revendaForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const selected = this.selectedRevenda();
    const request = selected ? this.service.update(selected.id, this.revendaForm.getRawValue()) : this.service.create(this.revendaForm.getRawValue());
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.revendaModalOpen.set(false);
        this.selectedRevenda.set(null);
        this.load();
        this.toastr.success('Revenda salva.', 'Cadastros');
      },
      error: () => {
        this.saving.set(false);
        this.toastr.error('Nao foi possivel salvar a revenda.', 'Erro');
      },
    });
  }
}
