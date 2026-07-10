import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

const API_URL = '/api';

export interface ECommerceUnit {
  empresaNumero: number;
  revendaNumero: number;
  vendedorCodigo?: number | null;
  vendedorNome: string;
  nome: string;
  nomeCurto: string;
  realizado: number;
  notasEmitidas: number;
  ticketMedio: number;
  custo: number;
  impostos: number;
  despesas: number;
  margemContribuicaoValor: number;
  margemContribuicaoPercentual: number;
  rentabilidadeValor: number;
  rentabilidadePercentual: number;
}

export interface ECommerceDashboard {
  atualizadoEm: string;
  unidades: ECommerceUnit[];
  evolucaoAnual: ECommerceAnnualSale[];
  evolucaoMensal: ECommerceMonthlySale[];
}

export interface ECommerceAnnualSale {
  ano: number;
  realizado: number;
  notasEmitidas: number;
  margemContribuicaoValor: number;
  margemContribuicaoPercentual: number;
}

export interface ECommerceMonthlySale {
  ano: number;
  mes: number;
  realizado: number;
  notasEmitidas: number;
  margemContribuicaoValor: number;
  margemContribuicaoPercentual: number;
}

export interface ECommerceSpreadsheetImport {
  dashboard: ECommerceDashboard;
  linhasImportadas: number;
  margemContribuicaoValor: number;
}

@Injectable({ providedIn: 'root' })
export class ECommerceService {
  constructor(private readonly http: HttpClient) {}

  load(filter: { dataInicio?: string; dataFim?: string; empresa?: number | null; revenda?: string[] | null } = {}): Observable<ECommerceDashboard> {
    const params: Record<string, string> = {};
    if (filter.dataInicio) {
      params['dataInicio'] = filter.dataInicio;
    }
    if (filter.dataFim) {
      params['dataFim'] = filter.dataFim;
    }
    if (filter.empresa) {
      params['empresa'] = String(filter.empresa);
    }
    if (filter.revenda?.length) {
      params['revenda'] = filter.revenda.join(',');
    }

    return this.http.get<ECommerceDashboard>(`${API_URL}/e-commerce`, { params });
  }

  importarPlanilha(arquivo: File): Observable<ECommerceSpreadsheetImport> {
    const formData = new FormData();
    formData.append('arquivo', arquivo, arquivo.name);
    return this.http.post<ECommerceSpreadsheetImport>(`${API_URL}/e-commerce/importar-planilha`, formData);
  }
}
