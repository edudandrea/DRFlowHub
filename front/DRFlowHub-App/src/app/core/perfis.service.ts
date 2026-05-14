import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

const API_URL = '/api';

export interface AcessoSistema {
  chave: string;
  nome: string;
  grupo: string;
}

export interface PerfilSistema {
  id: number;
  nome: string;
  padraoSistema: boolean;
  acessos: string[];
}

export interface PerfilSistemaPayload {
  nome: string;
  acessos: string[];
}

@Injectable({ providedIn: 'root' })
export class PerfisService {
  constructor(private readonly http: HttpClient) {}

  list(): Observable<PerfilSistema[]> {
    return this.http.get<PerfilSistema[]>(`${API_URL}/perfis`);
  }

  listAcessos(): Observable<AcessoSistema[]> {
    return this.http.get<AcessoSistema[]>(`${API_URL}/perfis/acessos`);
  }

  save(payload: PerfilSistemaPayload): Observable<PerfilSistema> {
    return this.http.post<PerfilSistema>(`${API_URL}/perfis`, payload);
  }
}
