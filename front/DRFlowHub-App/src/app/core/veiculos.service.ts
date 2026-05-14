import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { VeiculoEstoque } from './models';

const API_URL = '/api';

@Injectable({ providedIn: 'root' })
export class VeiculosService {
  constructor(private readonly http: HttpClient) {}

  listEstoque(filter: { empresa?: number | null; revenda?: number | null; busca?: string; reservado?: string } = {}): Observable<VeiculoEstoque[]> {
    const params: Record<string, string> = {};
    if (filter.empresa) {
      params['empresa'] = String(filter.empresa);
    }
    if (filter.revenda) {
      params['revenda'] = String(filter.revenda);
    }
    if (filter.busca?.trim()) {
      params['busca'] = filter.busca.trim();
    }
    if (filter.reservado) {
      params['reservado'] = filter.reservado;
    }

    return this.http.get<VeiculoEstoque[]>(`${API_URL}/veiculos/estoque`, { params });
  }

  updateReserva(chassi: string, empresa: number, reservado: boolean): Observable<VeiculoEstoque> {
    return this.http.post<VeiculoEstoque>(`${API_URL}/veiculos/estoque/${encodeURIComponent(chassi)}/reserva`, { empresa, reservado });
  }
}
