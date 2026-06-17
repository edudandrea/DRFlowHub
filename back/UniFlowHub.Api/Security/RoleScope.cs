
namespace UniFlowHub.Api.Security
{
    public static class RoleScope
    {
        
        public static bool IsAdmin(string? role)
            => string.Equals(role?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase);

        public static bool IsRH(string? role)
            => string.Equals(role?.Trim(), "RH", StringComparison.OrdinalIgnoreCase);

        public static bool IsTI(string? role)
            => string.Equals(role?.Trim(), "TI", StringComparison.OrdinalIgnoreCase);

        public static bool IsDiretoria(string? role)
            => string.Equals(role?.Trim(), "Diretoria", StringComparison.OrdinalIgnoreCase);

        public static bool IsCompras(string? role)
            => string.Equals(role?.Trim(), "Compras", StringComparison.OrdinalIgnoreCase);

        public static bool IsControladoria(string? role)
            => string.Equals(role?.Trim(), "Controladoria", StringComparison.OrdinalIgnoreCase);

        public static bool IsQualidadeNissan(string? role)
            => string.Equals(role?.Trim(), "Qualidade Nissan", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role?.Trim(), "QualidadeNissan", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role?.Trim(), "qualidadenissan", StringComparison.OrdinalIgnoreCase);

        public static bool IsGerentePecas(string? role)
            => string.Equals(role?.Trim(), "Gerente de Pecas", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role?.Trim(), "Gerente de Peças", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role?.Trim(), "GerentePecas", StringComparison.OrdinalIgnoreCase);

        public static bool IsGerenteGeralPecas(string? role)
            => string.Equals(role?.Trim(), "Gerente Geral de Pecas", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role?.Trim(), "Gerente Geral de Peças", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role?.Trim(), "GerenteGeralPecas", StringComparison.OrdinalIgnoreCase);

        public static bool IsVendedorPecas(string? role)
            => string.Equals(role?.Trim(), "Vendedor de Pecas", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role?.Trim(), "Vendedor de Peças", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role?.Trim(), "VendedorPecas", StringComparison.OrdinalIgnoreCase);

        public static bool IsGerente(string? role)
            => string.Equals(role?.Trim(), "Gerente", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role?.Trim(), "Gestor", StringComparison.OrdinalIgnoreCase);

            public static bool IsUser(string? role)
            => string.Equals(role?.Trim(), "Usuario", StringComparison.OrdinalIgnoreCase);        

        
    }
}
