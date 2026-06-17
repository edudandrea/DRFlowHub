import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

const API_URL = '/api';

export interface VeiculoAcessorioRanking {
  codigo: string;
  nome: string;
  categoria: string;
  quantidade: number;
  faturamento: number;
  margemPercentual: number;
  rentabilidade: number;
}

export interface VeiculosBiDashboard {
  filiais: VeiculoBiFilialVenda[];
  vendasDiarias: VeiculoBiVendaDiaria[];
  modelos: VeiculoBiModeloRanking[];
  vendedores: VeiculoBiVendedorMeta[];
  atualizadoEm: string;
}

export interface VeiculoBiFilialVenda {
  empresaNumero: number;
  empresaNome: string;
  revendaNumero: number;
  filial: string;
  metaNovos: number;
  metaVendaDireta: number;
  anunciadosNovos: number;
  faturadosNovos: number;
  anunciadosDireta: number;
  faturadosDireta: number;
  seminovos: number;
  propostas: number;
  baixados: number;
  faturamento: number;
  margem: number;
}

export interface VeiculoBiVendaDiaria {
  data: string;
  novos: number;
  vendaDireta: number;
  seminovos: number;
}

export interface VeiculoBiModeloRanking {
  modelo: string;
  familia: string;
  unidades: number;
  faturamento: number;
  margemPercentual: number;
}

export interface VeiculoBiVendedorMeta {
  vendedor: string;
  filial: string;
  meta: number;
  realizado: number;
  faturamento: number;
}

export interface VeiculosBiRetornoFiDashboard {
  contratos: number;
  retornoTotal: number;
  valorFinanciado: number;
  valorVenda: number;
  comissaoTotal: number;
  financeiras: VeiculosBiRetornoFiGrupo[];
  vendedores: VeiculosBiRetornoFiGrupo[];
  parcelas: VeiculosBiRetornoFiGrupo[];
  atualizadoEm: string;
}

export interface VeiculosBiRetornoFiGrupo {
  nome: string;
  quantidade: number;
  retorno: number;
  valorFinanciado: number;
  comissao: number;
}

@Injectable({ providedIn: 'root' })
export class VeiculosBiService {
  constructor(private readonly http: HttpClient) {}

  loadDashboard(filter: { dataInicio?: string; dataFim?: string; empresa?: number | null; revenda?: number[] | null } = {}): Observable<VeiculosBiDashboard> {
    return this.http.get<VeiculosBiDashboard>(`${API_URL}/veiculos-bi/dashboard`, { params: this.buildParams(filter) });
  }

  loadAcessorios(filter: { dataInicio?: string; dataFim?: string; empresa?: number | null; revenda?: number[] | null } = {}): Observable<VeiculoAcessorioRanking[]> {
    return this.http.get<VeiculoAcessorioRanking[]>(`${API_URL}/veiculos-bi/acessorios`, { params: this.buildParams(filter) });
  }

  loadRetornoFi(filter: { dataInicio?: string; dataFim?: string; empresa?: number | null; revenda?: number[] | null } = {}): Observable<VeiculosBiRetornoFiDashboard> {
    return this.http.get<VeiculosBiRetornoFiDashboard>(`${API_URL}/veiculos-bi/retorno-fi`, { params: this.buildParams(filter) });
  }

  private buildParams(filter: { dataInicio?: string; dataFim?: string; empresa?: number | null; revenda?: number[] | null } = {}): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value === undefined || value === null || value === '' || (Array.isArray(value) && !value.length)) {
        continue;
      }

      params = params.set(key, Array.isArray(value) ? value.join(',') : String(value));
    }

    return params;
  }
}
