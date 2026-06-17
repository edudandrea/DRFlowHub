using Microsoft.AspNetCore.Http;

namespace UniFlowHub.Api.Dtos.BaseConhecimentoTI
{
    public class BaseConhecimentoTICreateDto
    {
        public string Titulo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public IFormFile? Documento { get; set; }
    }
}
