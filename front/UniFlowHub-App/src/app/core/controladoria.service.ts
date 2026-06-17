import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { GuiaIcms } from './models';

const API_URL = '/api';

@Injectable({ providedIn: 'root' })
export class ControladoriaService {
  constructor(private readonly http: HttpClient) {}

  listGuiasIcms(filter: GuiaIcmsFilter = {}): Observable<GuiaIcms[]> {
    const params: Record<string, string> = {};
    if (filter.empresa?.trim()) {
      params['empresa'] = filter.empresa.trim();
    }
    if (filter.revenda?.trim()) {
      params['revenda'] = filter.revenda.trim();
    }
    if (filter.transacao?.trim()) {
      params['transacao'] = filter.transacao.trim();
    }
    if (filter.uf?.trim()) {
      params['uf'] = filter.uf.trim();
    }
    if (filter.dataInicio) {
      params['dataInicio'] = filter.dataInicio;
    }
    if (filter.dataFim) {
      params['dataFim'] = filter.dataFim;
    }

    return this.http.get<GuiaIcms[]>(`${API_URL}/controladoria/icms-guias`, { params });
  }

  updateGuiaIcmsPagamento(id: string, status: 'Pago' | 'Pendente'): Observable<GuiaIcms> {
    return this.http.post<GuiaIcms>(`${API_URL}/controladoria/icms-guias/${encodeURIComponent(id)}/pagamento`, { status });
  }

  updateGuiasIcmsPagamentoLote(guiaIds: string[], status: 'Pago' = 'Pago'): Observable<GuiaIcmsPagamentoLoteResult> {
    return this.http.post<GuiaIcmsPagamentoLoteResult>(`${API_URL}/controladoria/icms-guias/pagamento-lote`, { guiaIds, status });
  }
}

export interface GuiaIcmsFilter {
  empresa?: string;
  revenda?: string;
  transacao?: string;
  uf?: string;
  dataInicio?: string;
  dataFim?: string;
}

export interface GuiaIcmsPagamentoLoteResult {
  atualizadas: number;
  status: 'Pago' | string;
  dataPagamento?: string | null;
}
