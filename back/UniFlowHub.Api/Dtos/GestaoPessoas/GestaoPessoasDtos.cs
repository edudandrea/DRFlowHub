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
}
