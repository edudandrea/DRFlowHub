using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Dtos.EquipamentosTI;
using UniFlowHub.Api.Models;
using UniFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace UniFlowHub.Api.Services
{
    public class EquipamentosTIService
    {
        private readonly IEquipamentosTIRepo _repo;

        public EquipamentosTIService(IEquipamentosTIRepo repo)
        {
            _repo = repo;
        }

        public List<EquipamentoTIResponseDto> List(string role)
        {
            EnsureCanManage(role);

            return _repo.Query()
                .AsNoTracking()
                .Where(s => !s.Excluido)
                .OrderByDescending(s => s.DataMovimentacao)
                .Select(s => MapResponse(s))
                .ToList();
        }

        public EquipamentoTIResponseDto Add(EquipamentoTICreateDto dto, string role, int userId, string documentoUrl)
        {
            EnsureCanManage(role);
            var filialCompra = FirstFilled(dto.FilialCompra, dto.Origem, dto.Destino);
            var usuarioResponsavelNome = FirstFilled(dto.UsuarioResponsavelNome, dto.Responsavel);
            var usuarioResponsavelUnidade = FirstFilled(dto.UsuarioResponsavelUnidade, dto.Destino);
            Validate(dto.Tipo, dto.Patrimonio, filialCompra, dto.NotaFiscalCompra, usuarioResponsavelNome);

            var equipamento = new EquipamentoTI
            {
                Tipo = dto.Tipo.Trim(),
                Patrimonio = dto.Patrimonio.Trim(),
                Modelo = dto.Modelo.Trim(),
                Serial = dto.Serial.Trim(),
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Enviado" : dto.Status.Trim(),
                Origem = FirstFilled(dto.Origem, filialCompra, "TI"),
                Destino = FirstFilled(dto.Destino, usuarioResponsavelUnidade, filialCompra),
                Responsavel = FirstFilled(dto.Responsavel, usuarioResponsavelNome),
                FilialCompraId = dto.FilialCompraId,
                FilialCompra = filialCompra,
                NotaFiscalCompra = dto.NotaFiscalCompra.Trim(),
                UsuarioResponsavelId = dto.UsuarioResponsavelId,
                UsuarioResponsavelNome = usuarioResponsavelNome,
                UsuarioResponsavelEmail = dto.UsuarioResponsavelEmail.Trim(),
                UsuarioResponsavelDepartamento = dto.UsuarioResponsavelDepartamento.Trim(),
                UsuarioResponsavelUnidade = usuarioResponsavelUnidade,
                DataMovimentacao = DateTime.UtcNow,
                DataPrevistaRetorno = dto.DataPrevistaRetorno,
                Observacoes = dto.Observacoes.Trim(),
                DocumentoUrl = documentoUrl,
                Userid = userId
            };

            _repo.Add(equipamento);
            _repo.Save();

            return MapResponse(equipamento);
        }

        public void Excluir(int id, EquipamentoTIExcluirDto dto, string role, int userId)
        {
            EnsureCanManage(role);

            if (string.IsNullOrWhiteSpace(dto.Motivo))
                throw new InvalidOperationException("Motivo da exclusao e obrigatorio.");

            var equipamento = _repo.Query().FirstOrDefault(s => s.Id == id && !s.Excluido);
            if (equipamento is null)
                throw new KeyNotFoundException("Equipamento nao encontrado.");

            equipamento.Excluido = true;
            equipamento.MotivoExclusao = dto.Motivo.Trim();
            equipamento.DataExclusao = DateTime.UtcNow;
            equipamento.ExcluidoPorUserId = userId;
            equipamento.Status = "Excluido";

            _repo.Update(equipamento);
            _repo.Save();
        }

        public EquipamentoTIResponseDto Update(int id, EquipamentoTIUpdateDto dto, string role)
        {
            EnsureCanManage(role);
            var filialCompra = FirstFilled(dto.FilialCompra, dto.Origem, dto.Destino);
            var usuarioResponsavelNome = FirstFilled(dto.UsuarioResponsavelNome, dto.Responsavel);
            var usuarioResponsavelUnidade = FirstFilled(dto.UsuarioResponsavelUnidade, dto.Destino);
            Validate(dto.Tipo, dto.Patrimonio, filialCompra, dto.NotaFiscalCompra, usuarioResponsavelNome);

            var equipamento = _repo.Query().FirstOrDefault(s => s.Id == id);
            if (equipamento is null)
                throw new KeyNotFoundException("Equipamento nao encontrado.");

            equipamento.Tipo = dto.Tipo.Trim();
            equipamento.Patrimonio = dto.Patrimonio.Trim();
            equipamento.Modelo = dto.Modelo.Trim();
            equipamento.Serial = dto.Serial.Trim();
            equipamento.Status = dto.Status.Trim();
            equipamento.Origem = FirstFilled(dto.Origem, filialCompra, "TI");
            equipamento.Destino = FirstFilled(dto.Destino, usuarioResponsavelUnidade, filialCompra);
            equipamento.Responsavel = FirstFilled(dto.Responsavel, usuarioResponsavelNome);
            equipamento.FilialCompraId = dto.FilialCompraId;
            equipamento.FilialCompra = filialCompra;
            equipamento.NotaFiscalCompra = dto.NotaFiscalCompra.Trim();
            equipamento.UsuarioResponsavelId = dto.UsuarioResponsavelId;
            equipamento.UsuarioResponsavelNome = usuarioResponsavelNome;
            equipamento.UsuarioResponsavelEmail = dto.UsuarioResponsavelEmail.Trim();
            equipamento.UsuarioResponsavelDepartamento = dto.UsuarioResponsavelDepartamento.Trim();
            equipamento.UsuarioResponsavelUnidade = usuarioResponsavelUnidade;
            equipamento.DataPrevistaRetorno = dto.DataPrevistaRetorno;
            equipamento.Observacoes = dto.Observacoes.Trim();

            _repo.Update(equipamento);
            _repo.Save();

            return MapResponse(equipamento);
        }

        public EquipamentoTI GetAttachmentOwner(int id, string role)
        {
            EnsureCanManage(role);

            var equipamento = _repo.Query().AsNoTracking().FirstOrDefault(s => s.Id == id);
            if (equipamento is null)
                throw new KeyNotFoundException("Equipamento nao encontrado.");

            if (string.IsNullOrWhiteSpace(equipamento.DocumentoUrl))
                throw new FileNotFoundException("Este registro nao possui documento.");

            return equipamento;
        }

        private static void EnsureCanManage(string role)
        {
            if (!RoleScope.IsAdmin(role) && !RoleScope.IsTI(role))
                throw new UnauthorizedAccessException("Somente TI pode acessar o controle de equipamentos.");
        }

        private static void Validate(string tipo, string patrimonio, string filialCompra, string notaFiscalCompra, string usuarioResponsavelNome)
        {
            if (string.IsNullOrWhiteSpace(tipo))
                throw new InvalidOperationException("Tipo e obrigatorio.");

            if (string.IsNullOrWhiteSpace(patrimonio))
                throw new InvalidOperationException("Patrimonio e obrigatorio.");

            if (string.IsNullOrWhiteSpace(filialCompra))
                throw new InvalidOperationException("Filial da compra e obrigatoria.");

            if (string.IsNullOrWhiteSpace(notaFiscalCompra))
                throw new InvalidOperationException("Numero da nota fiscal e obrigatorio.");

            if (string.IsNullOrWhiteSpace(usuarioResponsavelNome))
                throw new InvalidOperationException("Usuario responsavel e obrigatorio.");
        }

        private static string FirstFilled(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }

        private static EquipamentoTIResponseDto MapResponse(EquipamentoTI s)
        {
            return new EquipamentoTIResponseDto
            {
                Id = s.Id,
                Tipo = s.Tipo,
                Patrimonio = s.Patrimonio,
                Modelo = s.Modelo,
                Serial = s.Serial,
                Status = s.Status,
                Origem = s.Origem,
                Destino = s.Destino,
                Responsavel = s.Responsavel,
                FilialCompraId = s.FilialCompraId,
                FilialCompra = s.FilialCompra,
                NotaFiscalCompra = s.NotaFiscalCompra,
                UsuarioResponsavelId = s.UsuarioResponsavelId,
                UsuarioResponsavelNome = s.UsuarioResponsavelNome,
                UsuarioResponsavelEmail = s.UsuarioResponsavelEmail,
                UsuarioResponsavelDepartamento = s.UsuarioResponsavelDepartamento,
                UsuarioResponsavelUnidade = s.UsuarioResponsavelUnidade,
                DataMovimentacao = s.DataMovimentacao,
                DataPrevistaRetorno = s.DataPrevistaRetorno,
                Observacoes = s.Observacoes,
                DocumentoUrl = s.DocumentoUrl,
                Excluido = s.Excluido,
                MotivoExclusao = s.MotivoExclusao,
                DataExclusao = s.DataExclusao,
                ExcluidoPorUserId = s.ExcluidoPorUserId,
                Userid = s.Userid
            };
        }
    }
}
