namespace UniFlowHub.Api.Dtos.GestaoPessoas
{
    public class GestaoPessoasEtapaDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string TipoProcesso { get; set; } = string.Empty;
        public int Ordem { get; set; }
        public bool Ativa { get; set; }
        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }
    }

    public class GestaoPessoasEtapaSaveDto
    {
        public string Nome { get; set; } = string.Empty;
        public string TipoProcesso { get; set; } = string.Empty;
        public int Ordem { get; set; }
        public bool Ativa { get; set; } = true;
    }

    public class GestaoPessoasProcessoDto
    {
        public int Id { get; set; }
        public string TipoProcesso { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Unidade { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string ColaboradorNome { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Prioridade { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public DateTime DataSolicitacao { get; set; }
        public DateTime? DataAprovacaoGestor { get; set; }
        public string AprovadorGestor { get; set; } = string.Empty;
        public string ObservacoesAprovacao { get; set; } = string.Empty;
        public DateTime? DataCancelamento { get; set; }
        public string MotivoCancelamento { get; set; } = string.Empty;
        public DateTime? DataConclusao { get; set; }
        public int? EtapaAtualId { get; set; }
        public string EtapaAtualNome { get; set; } = string.Empty;
        public int Userid { get; set; }
        public List<GestaoPessoasProcessoHistoricoDto> Historico { get; set; } = new();
    }

    public class GestaoPessoasProcessoCreateDto
    {
        public string TipoProcesso { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Unidade { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string ColaboradorNome { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Prioridade { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public int Userid { get; set; }
    }

    public class GestaoPessoasAprovacaoDto
    {
        public bool Aprovada { get; set; }
        public string ObservacoesAprovacao { get; set; } = string.Empty;
    }

    public class GestaoPessoasMovimentoDto
    {
        public string Observacoes { get; set; } = string.Empty;
    }

    public class GestaoPessoasCancelamentoDto
    {
        public string MotivoCancelamento { get; set; } = string.Empty;
    }

    public class GestaoPessoasProcessoHistoricoDto
    {
        public int Id { get; set; }
        public int EtapaId { get; set; }
        public string EtapaNome { get; set; } = string.Empty;
        public string Acao { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public int UsuarioId { get; set; }
        public string UsuarioNome { get; set; } = string.Empty;
        public DateTime DataMovimentacao { get; set; }
    }

    public class GestaoPessoasItemDto
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Tamanho { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public bool Ativo { get; set; }
        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }
    }

    public class GestaoPessoasItemSaveDto
    {
        public string Tipo { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Tamanho { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
    }

    public class GestaoPessoasCargoItemDto
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public string ItemNome { get; set; } = string.Empty;
        public string ItemTipo { get; set; } = string.Empty;
        public string ItemCodigo { get; set; } = string.Empty;
        public string ItemTamanho { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public bool Obrigatorio { get; set; }
    }

    public class GestaoPessoasCargoItemSaveDto
    {
        public int ItemId { get; set; }
        public int Quantidade { get; set; } = 1;
        public bool Obrigatorio { get; set; } = true;
    }

    public class GestaoPessoasCargoDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public bool Ativo { get; set; }
        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public List<GestaoPessoasCargoItemDto> Itens { get; set; } = new();
        public List<string> Acessos { get; set; } = new();
    }

    public class GestaoPessoasCargoSaveDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
        public List<GestaoPessoasCargoItemSaveDto> Itens { get; set; } = new();
        public List<string> Acessos { get; set; } = new();
    }

    public class GestaoPessoasColaboradorRetiradaDto
    {
        public int Id { get; set; }
        public int ColaboradorId { get; set; }
        public int ItemId { get; set; }
        public string ItemNome { get; set; } = string.Empty;
        public string ItemTipo { get; set; } = string.Empty;
        public string ItemCodigo { get; set; } = string.Empty;
        public string ItemTamanho { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public DateTime DataRetirada { get; set; }
        public DateTime? DataDevolucao { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
    }

    public class GestaoPessoasColaboradorRetiradaSaveDto
    {
        public int ItemId { get; set; }
        public int Quantidade { get; set; } = 1;
        public DateTime? DataRetirada { get; set; }
        public DateTime? DataDevolucao { get; set; }
        public string Status { get; set; } = "Retirado";
        public string Observacoes { get; set; } = string.Empty;
    }

    public class GestaoPessoasColaboradorDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public int? CargoId { get; set; }
        public string CargoNome { get; set; } = string.Empty;
        public int? UnidadeId { get; set; }
        public string UnidadeNome { get; set; } = string.Empty;
        public DateTime? DataNascimento { get; set; }
        public DateTime? DataAdmissao { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public List<GestaoPessoasCargoItemDto> ItensDoCargo { get; set; } = new();
        public List<GestaoPessoasColaboradorRetiradaDto> Retiradas { get; set; } = new();
    }

    public class GestaoPessoasColaboradorSaveDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public int? CargoId { get; set; }
        public int? UnidadeId { get; set; }
        public DateTime? DataNascimento { get; set; }
        public DateTime? DataAdmissao { get; set; }
        public string Status { get; set; } = "Ativo";
        public string Observacoes { get; set; } = string.Empty;
    }
}
