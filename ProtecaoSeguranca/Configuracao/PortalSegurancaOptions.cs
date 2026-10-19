namespace ProtecaoSeguranca.Configuracao
{
    /// <summary>
    /// Escolhas de cada API ao registrar o ProtecaoSeguranca.
    /// </summary>
    public class PortalSegurancaOptions
    {
        public bool UsarRateLimit { get; set; } = false;

        public bool UsarHeadersSeguranca { get; set; } = true;

        /// <summary>Validação dos arquivos enviados (upload). Regras na seção "Seguranca:UploadArquivos".</summary>
        public bool UsarValidacaoUpload { get; set; } = false;

        /// <summary>Criptografia das propriedades [Criptografado]. Sem campos marcados, não faz nada.</summary>
        public bool UsarCriptografia { get; set; } = true;
    }
}
