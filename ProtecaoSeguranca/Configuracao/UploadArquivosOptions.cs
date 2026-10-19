using System.Collections.Generic;

namespace ProtecaoSeguranca.Configuracao
{
    /// <summary>
    /// Opções da validação de upload de arquivos, lidas da seção "Seguranca:UploadArquivos" do appsettings (opcional).
    /// </summary>
    public class UploadArquivosOptions
    {
        public const string Secao = "Seguranca:UploadArquivos";

        /// <summary>Em <see cref="ExtensoesPermitidas"/>, aceita qualquer extensão que não esteja em <see cref="ExtensoesBloqueadas"/>.</summary>
        public const string QualquerExtensao = "*";

        public static readonly List<string> ExtensoesPermitidasPadrao = new List<string>
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp",
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp", ".rtf",
            ".txt", ".csv"
        };

        /// <summary>
        /// Executáveis, scripts de servidor e arquivos que o navegador interpreta (HTML/SVG).
        /// Valem em qualquer posição do nome: "foto.php.jpg" também é recusado.
        /// </summary>
        public static readonly List<string> ExtensoesBloqueadasPadrao = new List<string>
        {
            // Scripts de servidor
            ".php", ".php2", ".php3", ".php4", ".php5", ".php6", ".php7", ".php8", ".pht", ".phtm", ".phtml", ".phar", ".phps", ".inc",
            ".asp", ".aspx", ".ascx", ".ashx", ".asmx", ".asa", ".asax", ".axd", ".cer", ".cshtml", ".vbhtml", ".svc", ".soap", ".rem",
            ".config", ".cs", ".vb", ".master", ".skin", ".licx", ".resx",
            ".jsp", ".jspx", ".jsw", ".jsv", ".jspf", ".war", ".cfm", ".cfml", ".cfc",
            ".cgi", ".pl", ".pm", ".py", ".pyc", ".rb", ".lua", ".htaccess", ".htpasswd", ".user.ini",
            // Executáveis e scripts do sistema operacional
            ".exe", ".dll", ".com", ".scr", ".msi", ".msp", ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".sh", ".bash",
            ".vbs", ".vbe", ".js", ".jse", ".mjs", ".wsf", ".wsh", ".hta", ".jar", ".class", ".lnk", ".reg", ".inf", ".cpl", ".so", ".elf",
            // Conteúdo ativo no navegador (XSS armazenado)
            ".html", ".htm", ".xhtml", ".shtml", ".shtm", ".stm", ".svg", ".svgz", ".swf", ".xml", ".xsl", ".xslt"
        };

        public ModoProtecao Modo { get; set; } = ModoProtecao.Bloquear;

        /// <summary>
        /// Extensões aceitas (lista branca), com ou sem ponto. "*" aceita qualquer uma que não esteja bloqueada.
        /// Sem valor no appsettings, usa <see cref="ExtensoesPermitidasPadrao"/>.
        /// </summary>
        public List<string> ExtensoesPermitidas { get; set; }

        /// <summary>
        /// Extensões sempre recusadas, mesmo se estiverem nas permitidas.
        /// Sem valor no appsettings, usa <see cref="ExtensoesBloqueadasPadrao"/>.
        /// </summary>
        public List<string> ExtensoesBloqueadas { get; set; }

        /// <summary>Tamanho máximo de cada arquivo, em MB.</summary>
        public int TamanhoMaximoMb { get; set; } = 10;

        /// <summary>Quantidade máxima de arquivos numa mesma requisição.</summary>
        public int QuantidadeMaximaArquivos { get; set; } = 10;

        /// <summary>
        /// Confere se o início do arquivo bate com o formato da extensão (ex.: um ".pdf" precisa começar com "%PDF")
        /// e recusa executáveis disfarçados.
        /// </summary>
        public bool ValidarAssinatura { get; set; } = true;

        /// <summary>Procura código de servidor embutido no conteúdo (ex.: "&lt;?php" dentro de uma imagem).</summary>
        public bool ProcurarScripts { get; set; } = true;

        /// <summary>
        /// Regras específicas por trecho de path (a primeira que casar vale), para restringir mais um endpoint
        /// (ex.: "/documentos" só aceita PDF). Campos sem valor na regra usam os gerais.
        /// </summary>
        public List<RegraUpload> Regras { get; set; }
    }

    public class RegraUpload
    {
        /// <summary>Trecho do path, sem diferenciar maiúsculas (ex.: "/documentos").</summary>
        public string Path { get; set; }

        public List<string> ExtensoesPermitidas { get; set; }

        public int? TamanhoMaximoMb { get; set; }
    }
}
