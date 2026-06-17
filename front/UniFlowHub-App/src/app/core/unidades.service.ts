import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Empresa, EmpresaPayload, Unidade, UnidadePayload } from './models';

const API_URL = '/api';

@Injectable({ providedIn: 'root' })
export class UnidadesService {
  constructor(private readonly http: HttpClient) {}

  list(): Observable<Unidade[]> {
    return this.http.get<Unidade[]>(`${API_URL}/unidades`);
  }

  listEmpresas(): Observable<Empresa[]> {
    return this.http.get<Empresa[]>(`${API_URL}/unidades/empresas`);
  }

  createEmpresa(payload: EmpresaPayload): Observable<Empresa> {
    return this.http.post<Empresa>(`${API_URL}/unidades/empresas`, payload);
  }

  updateEmpresa(id: number, payload: EmpresaPayload): Observable<Empresa> {
    return this.http.put<Empresa>(`${API_URL}/unidades/empresas/${id}`, payload);
  }

  create(payload: UnidadePayload): Observable<Unidade> {
    return this.http.post<Unidade>(`${API_URL}/unidades`, payload);
  }

  update(id: number, payload: UnidadePayload): Observable<Unidade> {
    return this.http.put<Unidade>(`${API_URL}/unidades/${id}`, payload);
  }
}
