export type Role = string;

export interface User {
  id: number;
  nome: string;
  cpf: string;
  email: string;
  role: Role;
  perfis: Role[];
  departamento: string;
  cargo: string;
  ativo: boolean;
  unidadeId?: number | null;
  unidadeNome: string;
  dataNascimento: string;
  acessos: string[];
}

export type UserCreatePayload = Omit<User, 'id' | 'unidadeNome' | 'acessos'> & { senha: string };
export type UserUpdatePayload = Omit<User, 'id' | 'unidadeNome' | 'acessos'> & { senha?: string };
export type UserProfileUpdatePayload = Pick<User, 'nome' | 'cpf' | 'departamento' | 'cargo' | 'dataNascimento'>;

export interface Unidade {
  id: number;
  nome: string;
  empresaId?: number | null;
  empresaNumero: number;
  numeroRevenda: number;
  empresa: string;
  revenda: string;
  cnpj: string;
  endereco: string;
  dataCadastro: string;
}

export interface Empresa {
  id: number;
  numero: number;
  nome: string;
  logoUrl: string;
  dataCadastro: string;
}

export type EmpresaPayload = Pick<Empresa, 'numero' | 'nome' | 'logoUrl'>;
export type UnidadePayload = Pick<Unidade, 'empresaId' | 'numeroRevenda' | 'revenda' | 'cnpj' | 'endereco'>;

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: User;
}

export interface SolicitacaoRH {
  id: number;
  unidade: string;
  titulo: string;
  tipoSolicitacao: string;
  solicitante: string;
  departamento: string;
  descricao: string;
  anexossUrl: string;
  prioridade: string;
  responsavel: string;
  dataSolicitacao: string;
  dataEncerramento?: string | null;
  status: string;
  observacoes: string;
  observacoesEncerramento: string;
  satisfacaoNota?: number | null;
  satisfacaoComentario: string;
  dataAvaliacao?: string | null;
  dataAprovacao?: string | null;
  aprovada?: boolean | null;
  aprovador: string;
  observacoesAprovacao: string;
  aprovacaoPendente: boolean;
  avaliacaoPendente: boolean;
  userid: number;
}

export type SolicitacaoPayload = Omit<
  SolicitacaoRH,
  | 'id'
  | 'dataSolicitacao'
  | 'dataEncerramento'
  | 'observacoesEncerramento'
  | 'satisfacaoNota'
  | 'satisfacaoComentario'
  | 'dataAvaliacao'
  | 'dataAprovacao'
  | 'aprovada'
  | 'aprovador'
  | 'observacoesAprovacao'
  | 'aprovacaoPendente'
  | 'avaliacaoPendente'
>;

export interface ChamadoTI {
  id: number;
  titulo: string;
  categoria: string;
  descricao: string;
  solicitante: string;
  unidade: string;
  departamento: string;
  prioridade: string;
  status: string;
  responsavel: string;
  acessoRemotoUrl: string;
  acessoRemotoSenha: string;
  equipamentoNome: string;
  equipamentoIp: string;
  equipamentoSistemaOperacional: string;
  anexoImagemUrl: string;
  observacoes: string;
  observacoesEncerramento: string;
  satisfacaoNota?: number | null;
  satisfacaoComentario: string;
  dataAvaliacao?: string | null;
  dataAprovacao?: string | null;
  aprovada?: boolean | null;
  observacoesAprovacao: string;
  aprovacaoPendente: boolean;
  avaliacaoPendente: boolean;
  dataAbertura: string;
  dataPrimeiroEncerramento?: string | null;
  dataReabertura?: string | null;
  dataEncerramento?: string | null;
  ultimaMovimentacao: string;
  reaberto: boolean;
  userid: number;
}

export interface ChamadoTIComunicação {
  id: number;
  chamadoTIId: number;
  mensagem: string;
  autorNome: string;
  autorRole: string;
  autorUserId: number;
  dataCriacao: string;
  dataLeitura?: string | null;
}

export interface SolicitacaoRHComunicação {
  id: number;
  solicitacaoRHId: number;
  mensagem: string;
  autorNome: string;
  autorRole: string;
  autorUserId: number;
  dataCriacao: string;
}

export interface SolicitacaoCompraComunicação {
  id: number;
  solicitacaoCompraId: number;
  mensagem: string;
  autorNome: string;
  autorRole: string;
  autorUserId: number;
  dataCriacao: string;
}

export type ChamadoTIPayload = Omit<
  ChamadoTI,
  | 'id'
  | 'dataAbertura'
  | 'dataPrimeiroEncerramento'
  | 'dataReabertura'
  | 'dataEncerramento'
  | 'reaberto'
  | 'anexoImagemUrl'
  | 'observacoesEncerramento'
  | 'satisfacaoNota'
  | 'satisfacaoComentario'
  | 'dataAvaliacao'
  | 'avaliacaoPendente'
  | 'ultimaMovimentacao'
>;

export interface EquipamentoTI {
  id: number;
  tipo: string;
  patrimonio: string;
  modelo: string;
  serial: string;
  status: string;
  origem: string;
  destino: string;
  responsavel: string;
  filialCompraId?: number | null;
  filialCompra: string;
  notaFiscalCompra: string;
  usuarioResponsavelId?: number | null;
  usuarioResponsavelNome: string;
  usuarioResponsavelEmail: string;
  usuarioResponsavelDepartamento: string;
  usuarioResponsavelUnidade: string;
  dataMovimentacao: string;
  dataPrevistaRetorno?: string | null;
  observacoes: string;
  documentoUrl: string;
  excluido: boolean;
  motivoExclusao: string;
  dataExclusao?: string | null;
  excluidoPorUserId?: number | null;
  userid: number;
}

export type EquipamentoTIPayload = Omit<EquipamentoTI, 'id' | 'dataMovimentacao' | 'documentoUrl' | 'userid'>;

export interface BaseConhecimentoTI {
  id: number;
  titulo: string;
  categoria: string;
  descricao: string;
  tags: string;
  arquivoNome: string;
  arquivoUrl: string;
  arquivoContentType: string;
  dataCadastro: string;
  dataAtualizacao?: string | null;
  userid: number;
  autorNome: string;
}

export type BaseConhecimentoTIPayload = Pick<BaseConhecimentoTI, 'titulo' | 'categoria' | 'descricao' | 'tags'>;

export interface SolicitacaoCompra {
  id: number;
  titulo: string;
  categoria: string;
  descricao: string;
  solicitante: string;
  unidade: string;
  departamento: string;
  valorEstimado: number;
  fornecedorSugerido: string;
  prioridade: string;
  status: string;
  justificativa: string;
  observacoes: string;
  documentoUrl: string;
  dataSolicitacao: string;
  dataAprovacao?: string | null;
  dataEnvioCompras?: string | null;
  dataConclusao?: string | null;
  aprovador: string;
  comprador: string;
  observacoesAprovacao: string;
  observacoesCompras: string;
  userid: number;
}

export type SolicitacaoCompraPayload = Omit<
  SolicitacaoCompra,
  | 'id'
  | 'status'
  | 'documentoUrl'
  | 'dataSolicitacao'
  | 'dataAprovacao'
  | 'dataEnvioCompras'
  | 'dataConclusao'
  | 'aprovador'
  | 'comprador'
  | 'observacoesAprovacao'
  | 'observacoesCompras'
>;

export type SolicitacaoCompraUpdatePayload = Omit<
  SolicitacaoCompra,
  | 'id'
  | 'documentoUrl'
  | 'dataSolicitacao'
  | 'dataAprovacao'
  | 'dataEnvioCompras'
  | 'dataConclusao'
  | 'aprovador'
  | 'userid'
>;

export type GestaoPessoasTipoProcesso = 'Admissao' | 'Demissao';

export interface GestaoPessoasEtapa {
  id: number;
  nome: string;
  tipoProcesso: GestaoPessoasTipoProcesso;
  ordem: number;
  ativa: boolean;
  dataCadastro: string;
  dataAtualizacao?: string | null;
}

export type GestaoPessoasEtapaPayload = Pick<GestaoPessoasEtapa, 'nome' | 'tipoProcesso' | 'ordem' | 'ativa'>;

export interface GestaoPessoasHistorico {
  id: number;
  etapaId: number;
  etapaNome: string;
  acao: string;
  observacoes: string;
  usuarioId: number;
  usuarioNome: string;
  dataMovimentacao: string;
}

export interface GestaoPessoasProcesso {
  id: number;
  tipoProcesso: GestaoPessoasTipoProcesso;
  titulo: string;
  solicitante: string;
  unidade: string;
  departamento: string;
  colaboradorNome: string;
  cargo: string;
  descricao: string;
  prioridade: string;
  status: string;
  observacoes: string;
  dataSolicitacao: string;
  dataAprovacaoGestor?: string | null;
  aprovadorGestor: string;
  observacoesAprovacao: string;
  dataCancelamento?: string | null;
  motivoCancelamento: string;
  dataConclusao?: string | null;
  etapaAtualId?: number | null;
  etapaAtualNome: string;
  userid: number;
  historico: GestaoPessoasHistorico[];
}

export type GestaoPessoasProcessoPayload = Omit<
  GestaoPessoasProcesso,
  | 'id'
  | 'status'
  | 'dataSolicitacao'
  | 'dataAprovacaoGestor'
  | 'aprovadorGestor'
  | 'observacoesAprovacao'
  | 'dataCancelamento'
  | 'motivoCancelamento'
  | 'dataConclusao'
  | 'etapaAtualId'
  | 'etapaAtualNome'
  | 'historico'
>;

export type GestaoPessoasItemTipo = 'EPI' | 'Uniforme';

export interface GestaoPessoasItem {
  id: number;
  tipo: GestaoPessoasItemTipo;
  nome: string;
  codigo: string;
  tamanho: string;
  descricao: string;
  ativo: boolean;
  dataCadastro: string;
  dataAtualizacao?: string | null;
}

export type GestaoPessoasItemPayload = Pick<GestaoPessoasItem, 'tipo' | 'nome' | 'codigo' | 'tamanho' | 'descricao' | 'ativo'>;

export interface GestaoPessoasCargoItem {
  id: number;
  itemId: number;
  itemNome: string;
  itemTipo: GestaoPessoasItemTipo | string;
  itemCodigo: string;
  itemTamanho: string;
  quantidade: number;
  obrigatorio: boolean;
}

export type GestaoPessoasCargoItemPayload = Pick<GestaoPessoasCargoItem, 'itemId' | 'quantidade' | 'obrigatorio'>;

export interface GestaoPessoasCargo {
  id: number;
  nome: string;
  departamento: string;
  descricao: string;
  ativo: boolean;
  dataCadastro: string;
  dataAtualizacao?: string | null;
  itens: GestaoPessoasCargoItem[];
  acessos: string[];
}

export type GestaoPessoasCargoPayload = Pick<GestaoPessoasCargo, 'nome' | 'departamento' | 'descricao' | 'ativo'> & {
  itens: GestaoPessoasCargoItemPayload[];
  acessos: string[];
};

export interface GestaoPessoasColaboradorRetirada {
  id: number;
  colaboradorId: number;
  itemId: number;
  itemNome: string;
  itemTipo: GestaoPessoasItemTipo | string;
  itemCodigo: string;
  itemTamanho: string;
  quantidade: number;
  dataRetirada: string;
  dataDevolucao?: string | null;
  status: string;
  observacoes: string;
}

export type GestaoPessoasColaboradorRetiradaPayload = Pick<
  GestaoPessoasColaboradorRetirada,
  'itemId' | 'quantidade' | 'dataRetirada' | 'dataDevolucao' | 'status' | 'observacoes'
>;

export interface GestaoPessoasColaborador {
  id: number;
  nome: string;
  cpf: string;
  email: string;
  telefone: string;
  departamento: string;
  cargoId?: number | null;
  cargoNome: string;
  unidadeId?: number | null;
  unidadeNome: string;
  dataNascimento?: string | null;
  dataAdmissao?: string | null;
  status: string;
  observacoes: string;
  dataCadastro: string;
  dataAtualizacao?: string | null;
  itensDoCargo: GestaoPessoasCargoItem[];
  retiradas: GestaoPessoasColaboradorRetirada[];
}

export type GestaoPessoasColaboradorPayload = Omit<
  GestaoPessoasColaborador,
  | 'id'
  | 'cargoNome'
  | 'unidadeNome'
  | 'dataCadastro'
  | 'dataAtualizacao'
  | 'itensDoCargo'
  | 'retiradas'
>;

export interface GuiaIcms {
  id: string;
  documento: string;
  empresa: string;
  revenda: string;
  numeroNota: string;
  transacao: string;
  cnpj: string;
  competencia: string;
  dataVencimento?: string | null;
  dataPagamento?: string | null;
  valor: number;
  difal: number;
  fcp: number;
  uf: string;
  status: 'Pago' | 'Pendente' | string;
  observacoes: string;
}

export interface VeiculoEstoque {
  empresa: number;
  revenda: number;
  chassi: string;
  codigoVeiculo: string;
  modelo: string;
  descricaoModelo: string;
  cor: string;
  descricaoCor: string;
  reservado: boolean;
  origemReserva: string;
  dataReserva?: string | null;
}

export interface RepasseVeiculo {
  empresa: number;
  revenda: number;
  nomeEmpresa: string;
  nomeRevenda: string;
  modelo: string;
  placa: string;
  custoContabil: number;
  situacao: string;
  diasEstoque: number;
}

export interface RepasseDashboard {
  veiculos: RepasseVeiculo[];
  topDiasEstoque: RepasseVeiculo[];
  resumos: RepasseResumoEmpresa[];
}

export interface RepasseResumoEmpresa {
  empresa: number;
  nomeEmpresa: string;
  volumeDe: number;
  volumePara: number;
  custoDe: number;
  custoPara: number;
  ticketMedio: number;
  mediaGiroEstoque: number;
  distorcao: number;
  limiteAutorizado: number;
}

export interface RepasseVendedorResumo {
  vendedor: string;
  filial: string;
  quantidade: number;
  totalVenda: number;
  margem: number;
}

export interface RepasseVendaVendedorItem {
  empresa: number;
  revenda: number;
  nomeRevenda: string;
  vendedor: string;
  veiculo: string;
  placa: string;
  numeroNotaFiscal: string;
  totalVenda: number;
  margem: number;
  dataVenda: string;
}

export interface RepasseVendasVendedor {
  vendedores: RepasseVendedorResumo[];
  vendas: RepasseVendaVendedorItem[];
  atualizadoEm: string;
}

export interface CartaoPontoArquivo {
  id: number;
  nomeArquivo: string;
  cnpjUnidade: string;
  unidadeNome: string;
  dataImportacao: string;
  totalRegistros: number;
  totalFuncionarios: number;
}

export interface CartaoPontoFuncionario {
  nome: string;
  cpf: string;
  cnpjUnidade: string;
  unidadeNome: string;
  totalRegistros: number;
  totalDias: number;
  confirmadoPeloUsuario: boolean;
  precisaAjuste: boolean;
}

export interface CartaoPontoRegistro {
  id: number;
  arquivoId: number;
  funcionarioNome: string;
  cpf: string;
  data: string;
  horarioOriginal: string;
  horarioEditado: string;
  sequencia: number;
  confirmadoPeloUsuario: boolean;
  precisaAjuste: boolean;
}
