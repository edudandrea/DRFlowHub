using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UniFlowHub.Api.Data;
using UniFlowHub.Api.Dtos.CartaoPonto;
using UniFlowHub.Api.Models;
using UniFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace UniFlowHub.Api.Services
{
    public class CartaoPontoService
    {
        private readonly AppDbContext _context;

        public CartaoPontoService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CartaoPontoArquivoResponseDto> Importar(IFormFile arquivo, string role, int userId)
        {
            EnsureCanManage(role);
            if (arquivo.Length == 0)
                throw new InvalidOperationException("Arquivo TXT vazio.");

            var registros = new List<CartaoPontoRegistro>();
            using var reader = new StreamReader(arquivo.OpenReadStream(), detectEncodingFromByteOrderMarks: true);
            var linhas = await reader.ReadToEndAsync();
            var unidade = FindUnidadeByCnpj(linhas);
            var header = Array.Empty<string>();
            var sequenciaPorCpfData = new Dictionary<string, int>();

            foreach (var rawLine in linhas.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (LooksLikeHeader(line))
                {
                    header = SplitDelimited(line);
                    continue;
                }

                var parsed = ParseDelimited(line, header) ?? ParseAfd(line);
                if (parsed is null)
                    continue;

                var key = $"{parsed.Cpf}|{parsed.Data:yyyyMMdd}";
                sequenciaPorCpfData.TryGetValue(key, out var sequencia);
                sequenciaPorCpfData[key] = ++sequencia;

                registros.Add(new CartaoPontoRegistro
                {
                    FuncionarioNome = parsed.Nome,
                    Cpf = NormalizeDigits(parsed.Cpf),
                    Data = parsed.Data,
                    HorarioOriginal = parsed.Horario,
                    HorarioEditado = parsed.Horario,
                    Sequencia = sequencia,
                    LinhaOriginal = line
                });
            }

            if (registros.Count == 0)
                throw new InvalidOperationException("Nenhum registro valido foi encontrado no TXT.");

            var entidade = new CartaoPontoArquivo
            {
                NomeArquivo = Path.GetFileName(arquivo.FileName),
                CnpjUnidade = unidade?.Cnpj ?? FindFirstCnpj(linhas),
                UnidadeId = unidade?.Id,
                DataImportacao = DateTime.UtcNow,
                ImportadoPorUserId = userId,
                Registros = registros
            };

            _context.CartaoPontoArquivo.Add(entidade);
            await _context.SaveChangesAsync();
            return MapArquivo(entidade);
        }

        public List<CartaoPontoArquivoResponseDto> ListArquivos(string role, int userId)
        {
            EnsureCanAccess(role);
            var query = _context.CartaoPontoArquivo
                .AsNoTracking()
                .Include(a => a.Unidade)
                .Include(a => a.Registros)
                .AsQueryable();

            if (!CanManage(role))
            {
                var cpf = _context.User.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Cpf).FirstOrDefault() ?? string.Empty;
                var normalizedCpf = NormalizeDigits(cpf);
                query = query.Where(a => a.Registros.Any(r => r.Cpf == normalizedCpf));
            }

            return query
                .OrderByDescending(a => a.DataImportacao)
                .Select(a => MapArquivo(a))
                .ToList();
        }

        public List<CartaoPontoFuncionarioResponseDto> ListFuncionarios(int? arquivoId, string role, int userId)
        {
            EnsureCanAccess(role);
            var query = ScopedRegistros(arquivoId, role, userId);

            return query
                .Include(r => r.Arquivo)
                .ThenInclude(a => a!.Unidade)
                .GroupBy(r => new
                {
                    r.Cpf,
                    r.FuncionarioNome,
                    CnpjUnidade = r.Arquivo == null ? string.Empty : r.Arquivo.CnpjUnidade,
                    UnidadeNome = r.Arquivo == null || r.Arquivo.Unidade == null ? string.Empty : r.Arquivo.Unidade.Nome
                })
                .OrderBy(g => g.Key.FuncionarioNome)
                .Select(g => new CartaoPontoFuncionarioResponseDto
                {
                    Nome = g.Key.FuncionarioNome,
                    Cpf = g.Key.Cpf,
                    CnpjUnidade = g.Key.CnpjUnidade,
                    UnidadeNome = g.Key.UnidadeNome,
                    TotalRegistros = g.Count(),
                    TotalDias = g.Select(r => r.Data.Date).Distinct().Count(),
                    ConfirmadoPeloUsuario = g.All(r => r.ConfirmadoPeloUsuario),
                    PrecisaAjuste = g.Any(r => r.PrecisaAjuste)
                })
                .ToList();
        }

        public List<CartaoPontoRegistroResponseDto> ListRegistros(string cpf, int? arquivoId, string role, int userId)
        {
            EnsureCanAccess(role);
            var normalizedCpf = NormalizeDigits(cpf);
            var query = ScopedRegistros(arquivoId, role, userId).Where(r => r.Cpf == normalizedCpf);

            if (!CanManage(role))
                EnsureOwnCpf(normalizedCpf, userId);

            return query
                .OrderBy(r => r.Data)
                .ThenBy(r => r.HorarioEditado)
                .ThenBy(r => r.Sequencia)
                .Select(r => MapRegistro(r))
                .ToList();
        }

        public CartaoPontoRegistroResponseDto UpdateRegistro(int id, CartaoPontoRegistroUpdateDto dto, string role, int userId)
        {
            EnsureCanManage(role);
            var registro = _context.CartaoPontoRegistro.FirstOrDefault(r => r.Id == id)
                ?? throw new KeyNotFoundException("Registro de ponto nao encontrado.");

            var horario = dto.HorarioEditado?.Trim() ?? string.Empty;
            if (!TimeSpan.TryParseExact(horario, @"hh\:mm", CultureInfo.InvariantCulture, out _))
                throw new InvalidOperationException("Informe o horario no formato HH:mm.");

            registro.HorarioEditado = horario;
            registro.DataEdicao = DateTime.UtcNow;
            registro.EditadoPorUserId = userId;
            _context.SaveChanges();

            return MapRegistro(registro);
        }

        public void ResponderFuncionario(string cpf, int? arquivoId, string? mes, CartaoPontoRespostaUsuarioDto dto, string role, int userId)
        {
            EnsureCanAccess(role);
            var normalizedCpf = NormalizeDigits(cpf);
            EnsureOwnCpf(normalizedCpf, userId);

            var query = ScopedRegistros(arquivoId, role, userId).Where(r => r.Cpf == normalizedCpf);
            if (!string.IsNullOrWhiteSpace(mes) && DateTime.TryParseExact($"{mes}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
            {
                var nextMonth = month.AddMonths(1);
                query = query.Where(r => r.Data >= month && r.Data < nextMonth);
            }

            var registros = query.ToList();

            if (registros.Count == 0)
                throw new KeyNotFoundException("Cartao ponto nao encontrado.");

            foreach (var registro in registros)
            {
                registro.ConfirmadoPeloUsuario = !dto.PrecisaAjuste;
                registro.PrecisaAjuste = dto.PrecisaAjuste;
                registro.DataRespostaUsuario = DateTime.UtcNow;
            }

            _context.SaveChanges();
        }

        private IQueryable<CartaoPontoRegistro> ScopedRegistros(int? arquivoId, string role, int userId)
        {
            var query = _context.CartaoPontoRegistro.AsQueryable();
            if (arquivoId.HasValue)
                query = query.Where(r => r.CartaoPontoArquivoId == arquivoId.Value);

            if (!CanManage(role))
            {
                var cpf = _context.User.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Cpf).FirstOrDefault() ?? string.Empty;
                query = query.Where(r => r.Cpf == NormalizeDigits(cpf));
            }

            return query;
        }

        private Unidade? FindUnidadeByCnpj(string content)
        {
            var cnpjs = Regex.Matches(content, @"\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}")
                .Select(match => NormalizeDigits(match.Value))
                .Distinct()
                .ToList();

            if (cnpjs.Count == 0)
                return null;

            return _context.Unidade
                .AsNoTracking()
                .AsEnumerable()
                .FirstOrDefault(unidade => cnpjs.Contains(NormalizeDigits(unidade.Cnpj)));
        }

        private static string FindFirstCnpj(string content)
        {
            return Regex.Matches(content, @"\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}")
                .Select(match => NormalizeDigits(match.Value))
                .FirstOrDefault() ?? string.Empty;
        }

        private void EnsureOwnCpf(string cpf, int userId)
        {
            var userCpf = _context.User.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Cpf).FirstOrDefault() ?? string.Empty;
            if (NormalizeDigits(userCpf) != cpf)
                throw new UnauthorizedAccessException("Voce so pode visualizar o seu proprio cartao ponto.");
        }

        private static ParsedRegistro? ParseDelimited(string line, string[] header)
        {
            var parts = SplitDelimited(line);
            if (parts.Length < 3)
                return null;

            string GetByHeader(params string[] names)
            {
                for (var i = 0; i < header.Length && i < parts.Length; i++)
                {
                    var key = NormalizeText(header[i]);
                    if (names.Any(name => key.Contains(name)))
                        return parts[i].Trim();
                }

                return string.Empty;
            }

            var cpf = GetByHeader("cpf", "documento") is { Length: > 0 } cpfByHeader
                ? cpfByHeader
                : parts.FirstOrDefault(p => NormalizeDigits(p).Length == 11) ?? string.Empty;
            var nome = GetByHeader("nome", "funcionario", "colaborador");
            var dataText = GetByHeader("data");
            var horaText = GetByHeader("hora", "horario", "marcacao");

            if (string.IsNullOrWhiteSpace(nome))
                nome = parts.FirstOrDefault(p => Regex.IsMatch(p, "[A-Za-zÀ-ÿ]")) ?? "Funcionario sem nome";
            if (string.IsNullOrWhiteSpace(dataText))
                dataText = parts.FirstOrDefault(IsDate) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(horaText))
                horaText = parts.FirstOrDefault(IsTime) ?? string.Empty;

            return CreateParsed(nome, cpf, dataText, horaText);
        }

        private static ParsedRegistro? ParseAfd(string line)
        {
            if (line.Length < 22)
                return null;

            if (line.Length >= 34 && line[9] == '3')
            {
                var fixedDate = line.Substring(10, 8);
                var fixedTime = line.Substring(18, 4);
                var fixedCpf = Regex.Matches(line, @"\d{11}").Cast<Match>().FirstOrDefault();
                if (fixedCpf is not null)
                    return CreateParsed("Funcionario " + fixedCpf.Value, fixedCpf.Value, fixedDate, fixedTime);
            }

            var dateMatch = Regex.Match(line, @"(?<!\d)(\d{2})(\d{2})(\d{4})(?!\d)");
            var timeMatch = Regex.Match(line, @"(?<!\d)([01]\d|2[0-3])([0-5]\d)(?!\d)");
            var cpfMatch = Regex.Matches(line, @"\d{11}").Cast<Match>().FirstOrDefault();
            if (!dateMatch.Success || !timeMatch.Success || cpfMatch is null)
                return null;

            return CreateParsed(
                "Funcionario " + cpfMatch.Value,
                cpfMatch.Value,
                $"{dateMatch.Groups[1].Value}/{dateMatch.Groups[2].Value}/{dateMatch.Groups[3].Value}",
                $"{timeMatch.Groups[1].Value}:{timeMatch.Groups[2].Value}");
        }

        private static ParsedRegistro? CreateParsed(string nome, string cpf, string dataText, string horaText)
        {
            cpf = NormalizeDigits(cpf);
            if (cpf.Length != 11 || !TryParseDate(dataText, out var data) || !TryParseTime(horaText, out var hora))
                return null;

            return new ParsedRegistro(nome.Trim(), cpf, data, hora);
        }

        private static string[] SplitDelimited(string line)
            => line.Split(new[] { ';', '\t', ',' }, StringSplitOptions.TrimEntries);

        private static bool LooksLikeHeader(string line)
        {
            var normalized = NormalizeText(line);
            return normalized.Contains("cpf") && (normalized.Contains("data") || normalized.Contains("hora"));
        }

        private static bool TryParseDate(string value, out DateTime date)
            => DateTime.TryParseExact(value.Trim(), new[] { "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "ddMMyyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        private static bool TryParseTime(string value, out string time)
        {
            time = string.Empty;
            var text = value.Trim();
            if (Regex.IsMatch(text, @"^\d{4}$"))
                text = text.Insert(2, ":");

            if (!TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out var parsed))
                return false;

            time = parsed.ToString(@"hh\:mm");
            return true;
        }

        private static bool IsDate(string value) => TryParseDate(value, out _);
        private static bool IsTime(string value) => TryParseTime(value, out _);

        private static string NormalizeDigits(string value)
            => Regex.Replace(value ?? string.Empty, @"\D", string.Empty);

        private static string NormalizeText(string value)
            => string.Concat((value ?? string.Empty).Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();

        private static void EnsureCanAccess(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new UnauthorizedAccessException("Usuario invalido.");
        }

        private static void EnsureCanManage(string role)
        {
            if (!CanManage(role))
                throw new UnauthorizedAccessException("Somente Admin e RH podem administrar cartoes ponto.");
        }

        private static bool CanManage(string role)
            => RoleScope.IsAdmin(role) || RoleScope.IsRH(role);

        private static CartaoPontoArquivoResponseDto MapArquivo(CartaoPontoArquivo arquivo)
            => new()
            {
                Id = arquivo.Id,
                NomeArquivo = arquivo.NomeArquivo,
                CnpjUnidade = arquivo.CnpjUnidade,
                UnidadeNome = arquivo.Unidade?.Nome ?? string.Empty,
                DataImportacao = arquivo.DataImportacao,
                TotalRegistros = arquivo.Registros.Count,
                TotalFuncionarios = arquivo.Registros.Select(r => r.Cpf).Distinct().Count()
            };

        private static CartaoPontoRegistroResponseDto MapRegistro(CartaoPontoRegistro registro)
            => new()
            {
                Id = registro.Id,
                ArquivoId = registro.CartaoPontoArquivoId,
                FuncionarioNome = registro.FuncionarioNome,
                Cpf = registro.Cpf,
                Data = registro.Data,
                HorarioOriginal = registro.HorarioOriginal,
                HorarioEditado = registro.HorarioEditado,
                Sequencia = registro.Sequencia,
                ConfirmadoPeloUsuario = registro.ConfirmadoPeloUsuario,
                PrecisaAjuste = registro.PrecisaAjuste
            };

        private sealed record ParsedRegistro(string Nome, string Cpf, DateTime Data, string Horario);
    }
}
