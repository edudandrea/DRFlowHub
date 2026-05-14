import { Routes } from '@angular/router';
import { accessGuard, authGuard } from './core/auth.guard';
import { AdminPage } from './pages/admin/admin';
import { AdminDashboardPage } from './pages/admin-dashboard/admin-dashboard';
import { ComprasPage } from './pages/compras/compras';
import { ControladoriaComponent } from './pages/controladoria/controladoria.component';
import { CartaoPontoPage } from './pages/cartao-ponto/cartao-ponto';
import { EquipamentosTIPage } from './pages/equipamentos-ti/equipamentos-ti';
import { HubPage } from './pages/hub/hub';
import { LoginPage } from './pages/login/login';
import { PecasBiPage } from './pages/pecas-bi/pecas-bi';
import { SolicitacoesPage } from './pages/solicitacoes/solicitacoes';
import { TiPage } from './pages/ti/ti';
import { UsuariosPage } from './pages/usuarios/usuarios';
import { VeiculosComponent } from './pages/veiculos/veiculos.component';
import { VeiculosBiPage } from './pages/veiculos-bi/veiculos-bi';
import { EmpresasRevendasPage } from './pages/empresas-revendas/empresas-revendas';
import { PerfisPage } from './pages/perfis/perfis';

export const routes: Routes = [
  { path: 'login', component: LoginPage },
  {
    path: 'hub',
    component: HubPage,
    canActivate: [authGuard],
  },
  {
    path: 'solicitacoes',
    component: SolicitacoesPage,
    canActivate: [authGuard],
  },
  {
    path: 'admin',
    component: AdminDashboardPage,
    canActivate: [accessGuard(['dashboard-admin'])],
  },
  {
    path: 'rh',
    component: AdminPage,
    canActivate: [accessGuard(['rh-admin'])],
  },
  {
    path: 'rh/cartao-ponto',
    component: CartaoPontoPage,
    canActivate: [accessGuard(['cartao-ponto'])],
  },
  {
    path: 'ti',
    component: TiPage,
    canActivate: [accessGuard(['ti'])],
  },
  {
    path: 'ti/equipamentos',
    component: EquipamentosTIPage,
    canActivate: [accessGuard(['equipamentos-ti'])],
  },
  {
    path: 'compras',
    component: ComprasPage,
    canActivate: [accessGuard(['compras'])],
  },
  {
    path: 'controladoria',
    component: ControladoriaComponent,
    canActivate: [accessGuard(['controladoria'])],
  },
  {
    path: 'vendas-pecas',
    component: PecasBiPage,
    canActivate: [accessGuard(['vendas-pecas'])],
  },
  {
    path: 'estoque/veiculos',
    component: VeiculosComponent,
    canActivate: [accessGuard(['veiculos'])],
  },
  {
    path: 'veiculos/bi-vendas',
    component: VeiculosBiPage,
    canActivate: [accessGuard(['veiculos-bi'])],
  },
  {
    path: 'usuarios',
    component: UsuariosPage,
    canActivate: [accessGuard(['usuarios'])],
  },
  {
    path: 'cadastros/empresas-revendas',
    component: EmpresasRevendasPage,
    canActivate: [accessGuard(['empresas-revendas'])],
  },
  {
    path: 'cadastros/perfis',
    component: PerfisPage,
    canActivate: [accessGuard(['perfis'])],
  },
  { path: '', redirectTo: 'hub', pathMatch: 'full' },
  { path: '**', redirectTo: '' },
];
