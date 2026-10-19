# Validação de upload de arquivos

Recusa arquivos maliciosos enviados à aplicação — por exemplo, um script `.php` ou `.aspx` que depois seria executado no servidor (web shell), um executável disfarçado de PDF ou uma imagem com código escondido.

## Disponibilidade

| Plataforma | Como ligar | Padrão |
|---|---|---|
| ASP.NET Core | `app.UsePortalSeguranca(o => o.UsarValidacaoUpload = true)` ou `app.UseValidacaoUpload()` | **desligado** |
| .NET Framework | `Seguranca:UsarValidacaoUpload = true` com o `PortalSegurancaModule`, ou `UploadArquivosModule` no `web.config` | **desligado** |

Instalação: ver [Instalação e configuração](00-instalacao.md).

## O que é verificado

Vale para requisições `POST`, `PUT` ou `PATCH` com `multipart/form-data`; as demais passam direto.
Cada arquivo passa por estas verificações, nesta ordem:

1. **Nome**: recusa nome vazio, sem extensão, com caracteres de controle ou com `:` (ex.: `shell.php::$DATA`). Pontos e espaços no fim são ignorados, como faz o Windows (`shell.php.` conta como `.php`).
2. **Extensões bloqueadas**: valem **em qualquer posição do nome** (`foto.php.jpg` é recusado) e **sempre vencem** a lista de permitidas.
3. **Extensões permitidas** (lista branca).
4. **Tamanho** de cada arquivo e **quantidade** de arquivos na requisição.
5. **Assinatura**: o início do arquivo precisa bater com o formato da extensão (um `.pdf` começa com `%PDF`, um `.png` com `‰PNG`...). Executáveis Windows/Linux e scripts `#!` são recusados com qualquer extensão.
6. **Código embutido**: procura em todo o conteúdo trechos como `<?php`, `<script`, `<%@`, `runat="server"` (ex.: PHP escondido nos metadados de uma imagem). Em arquivos de texto (`.txt`, `.csv`) procura também os curtos, como `<?=` e `<%=`.

### Extensões permitidas por padrão

| Tipo | Extensões |
|---|---|
| PDF | `.pdf` |
| Imagens | `.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`, `.tif`, `.tiff`, `.webp` |
| Office / LibreOffice | `.doc`, `.docx`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, `.odt`, `.ods`, `.odp`, `.rtf` |
| Texto | `.txt`, `.csv` |

### Extensões bloqueadas por padrão

| Tipo | Exemplos |
|---|---|
| Scripts de servidor | `.php` (e variações), `.asp`, `.aspx`, `.ashx`, `.asmx`, `.cshtml`, `.config`, `.cs`, `.jsp`, `.war`, `.cfm`, `.cgi`, `.pl`, `.py`, `.rb`, `.htaccess`... |
| Executáveis e scripts do SO | `.exe`, `.dll`, `.msi`, `.bat`, `.cmd`, `.ps1`, `.sh`, `.vbs`, `.js`, `.jar`, `.lnk`, `.reg`, `.so`... |
| Conteúdo ativo no navegador | `.html`, `.htm`, `.xhtml`, `.svg`, `.svgz`, `.swf`, `.xml`, `.xsl`, `.xslt` |

A lista completa está em `UploadArquivosOptions.ExtensoesBloqueadasPadrao`.

## Resposta ao recusar

| Motivo | Status | Corpo |
|---|---|---|
| Arquivo acima do tamanho | **413** | `{"mensagem":"Arquivo excede o tamanho máximo permitido."}` |
| Qualquer outro | **400** | `{"mensagem":"Arquivo não permitido."}` |

O motivo detalhado vai **só para o log**, para não ensinar o atacante a contornar a validação:
```
Upload de arquivo recusado, requisição bloqueada. IP: 200.x.x.x X-Forwarded-For:  POST /api/anexo Campo: arquivo Arquivo: foto.php.jpg Motivo: extensão bloqueada (.php)
```

## Configuração

Sem configuração, valem os padrões acima. Todos os campos são opcionais.

### ASP.NET Core (`appsettings.json`)

```json
"Seguranca": {
  "UploadArquivos": {
    "Modo": "Bloquear",
    "ExtensoesPermitidas": [ ".pdf", ".jpg", ".jpeg", ".png", ".docx", ".xlsx" ],
    "ExtensoesBloqueadas": null,
    "TamanhoMaximoMb": 10,
    "QuantidadeMaximaArquivos": 10,
    "ValidarAssinatura": true,
    "ProcurarScripts": true,
    "Regras": [
      { "Path": "/documentos", "ExtensoesPermitidas": [ ".pdf" ], "TamanhoMaximoMb": 5 },
      { "Path": "/fotos", "ExtensoesPermitidas": [ ".jpg", ".jpeg", ".png" ] }
    ]
  }
}
```

### .NET Framework (`web.config`)

As regras usam o formato `path=extensões|tamanho`, com extensões separadas por `|` e o tamanho com sufixo `MB`:
```xml
<appSettings>
  <add key="Seguranca:UsarValidacaoUpload" value="true" />
  <add key="Seguranca:UploadArquivos:Modo" value="Bloquear" />
  <add key="Seguranca:UploadArquivos:ExtensoesPermitidas" value=".pdf,.jpg,.jpeg,.png,.docx,.xlsx" />
  <add key="Seguranca:UploadArquivos:TamanhoMaximoMb" value="10" />
  <add key="Seguranca:UploadArquivos:QuantidadeMaximaArquivos" value="10" />
  <add key="Seguranca:UploadArquivos:Regras" value="/documentos=.pdf|5MB,/fotos=.jpg|.jpeg|.png" />
</appSettings>
```

### Opções

| Opção | Padrão | Descrição |
|---|---|---|
| `Modo` | `Bloquear` | `SomenteLog` só registra, sem recusar. Ver [Modo somente monitorar](07-modo-monitoramento.md). |
| `ExtensoesPermitidas` | lista acima | **Substitui** a lista padrão (com ou sem ponto). `"*"` aceita qualquer extensão que não esteja bloqueada — use só se não houver alternativa. |
| `ExtensoesBloqueadas` | lista acima | **Substitui** a lista padrão. Para acrescentar algo, copie `UploadArquivosOptions.ExtensoesBloqueadasPadrao` e inclua; evite remover itens. |
| `TamanhoMaximoMb` | `10` | Tamanho máximo de cada arquivo. |
| `QuantidadeMaximaArquivos` | `10` | Arquivos por requisição. |
| `ValidarAssinatura` | `true` | Confere o início do arquivo e recusa executáveis disfarçados. |
| `ProcurarScripts` | `true` | Procura código de servidor embutido. |
| `Regras` | nenhuma | Restrições por trecho do path (a primeira que casar vale). Campos não informados usam os gerais; as extensões bloqueadas valem sempre. |

## Regras por endpoint

Use `Regras` para restringir mais um endpoint do que o geral. Exemplo: a API aceita vários formatos, mas `/api/servidor/documentos` só PDF até 5 MB e `/api/servidor/foto` só imagens:
```json
"Regras": [
  { "Path": "/documentos", "ExtensoesPermitidas": [ ".pdf" ], "TamanhoMaximoMb": 5 },
  { "Path": "/foto", "ExtensoesPermitidas": [ ".jpg", ".jpeg", ".png" ] }
]
```
A comparação é por trecho do path, sem diferenciar maiúsculas: `/foto` casa com `/api/servidor/foto` e também com `/api/fotografia`. Prefira trechos específicos e coloque as regras mais específicas primeiro.

## Importante

- **O limite do servidor é avaliado antes.** No IIS, `maxRequestLength` (KB) e `maxAllowedContentLength` (bytes); no Kestrel, `MaxRequestBodySize`. Ajuste-os para pouco acima do maior upload esperado; se forem menores que `TamanhoMaximoMb`, o servidor recusa antes da DLL.
  ```xml
  <system.web>
    <httpRuntime maxRequestLength="20480" />                         <!-- 20 MB -->
  </system.web>
  <system.webServer>
    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="20971520" />         <!-- 20 MB -->
      </requestFiltering>
    </security>
  </system.webServer>
  ```
- **A validação não substitui os cuidados no endpoint:**
  - salvar os arquivos **fora** da pasta publicada do site (ou numa pasta sem permissão de execução);
  - gerar o nome no servidor (ex.: `Guid`) em vez de usar o nome enviado;
  - nunca servir o arquivo com o `Content-Type` informado pelo cliente.
- **Uploads fora de `multipart/form-data`** (ex.: arquivo em base64 dentro de um JSON) não passam por esta validação; nesses casos valide no próprio endpoint.
- No .NET Framework, `Request.Files` lê o corpo uma vez e o mantém disponível; a aplicação continua lendo os arquivos normalmente.
