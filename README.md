# ProtecaoSeguranca

DLL de cibersegurança para APIs e sites .NET, distribuída como pacote NuGet no feed RepoSTI.
Um único pacote atende projetos novos e antigos; o NuGet escolhe sozinho a versão certa da DLL:

| Projeto | Versão usada do pacote | Como registrar |
|---|---|---|
| ASP.NET (.NET Framework 4.5.2 a 4.8.1): MVC, Web API, Web Forms, ASMX | `net452` | Módulos no `web.config` |
| ASP.NET Core 2.2 | `netcoreapp2.2` | Middleware no `Startup` |
| ASP.NET Core 3.1 e 5 | `netcoreapp3.1` | Middleware no `Startup` |
| ASP.NET Core 6, 7, 8 e 9 | `net6.0` | Middleware no `Program.cs`/`Startup` |
| ASP.NET Core 10 em diante | `net10.0` | Middleware no `Program.cs` |

Proteções (todas opcionais):
- **SQL Injection**: bloqueia com 400 requisições com padrões de SQL Injection no path, na query string ou no corpo (JSON/formulário).
- **Ocultar erros 5xx**: troca a mensagem de erros internos por uma mensagem genérica com código de correlação (header `X-Correlation-Id`); o erro real vai para o log.
- **Rate limit** (desligado por padrão): limite por IP em janela de 1 minuto; responde 429 com `Retry-After`.
- **Headers de segurança**: adiciona `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy`, `Permissions-Policy` e `Strict-Transport-Security` a todas as respostas, e remove `Server`, `X-Powered-By` e `X-AspNet-Version`. Headers já definidos pela aplicação não são sobrescritos.
- **Validação de upload** (desligada por padrão): recusa arquivos enviados com extensão fora da lista permitida, extensões perigosas em qualquer posição do nome (`foto.php.jpg`), conteúdo que não corresponde à extensão, executáveis disfarçados e código de servidor embutido (`<?php` dentro de uma imagem). Ver [Validação de upload](#validação-de-upload).
- **Criptografia de dados**: propriedades marcadas com `[Criptografado]` saem criptografadas para o front e são descriptografadas quando voltam. Ver [Criptografia de dados](#criptografia-de-dados).

## Modo somente monitorar

O SQL Injection, o rate limit e a validação de upload têm dois modos:
- **`Bloquear`** (padrão): registra no log e bloqueia a requisição.
- **`SomenteLog`**: só registra no log e deixa a requisição seguir. A mensagem começa com `[MONITORAMENTO]` e termina em `(não bloqueado)`, o que facilita filtrar.

Use o `SomenteLog` para ligar uma proteção em produção sem risco de barrar usuários legítimos (ex.: um nome com apóstrofo, como D'Ávila). Deixe rodando por uma ou duas semanas, analise os logs com `[MONITORAMENTO]`, ajuste o que for preciso e só então troque para `Bloquear`.

```json
"Seguranca": {
  "SqlInjection": { "Modo": "SomenteLog" },
  "RateLimit": { "Modo": "SomenteLog" }
}
```
No .NET Framework, as mesmas chaves vão no `appSettings` (`Seguranca:SqlInjection:Modo` e `Seguranca:RateLimit:Modo`).

Exemplo de log:
```
[MONITORAMENTO] Possível SQL Injection (não bloqueado). IP: 200.x.x.x X-Forwarded-For:  GET /api/servidor Campo: nome Valor: D'Ávila
```

## Como usar numa API ASP.NET Core

1. Referenciar o pacote:
   ```xml
   <PackageReference Include="ProtecaoSeguranca" Version="1.4.0" />
   ```
2. Registrar depois do `UseCors`/`UseAuthentication` e antes do `UseMvc`/`MapControllers`:
   ```csharp
   using ProtecaoSeguranca.Extensions;

   app.UsePortalSeguranca();                                      // headers + SQL Injection + ocultar erros
   app.UsePortalSeguranca(o => o.UsarRateLimit = true);           // ... + rate limit
   app.UsePortalSeguranca(o => o.UsarValidacaoUpload = true);     // ... + validação de upload
   app.UsePortalSeguranca(o => o.UsarHeadersSeguranca = false);   // sem os headers de segurança
   app.UsePortalSeguranca(o => o.UsarCriptografia = false);       // sem a criptografia de [Criptografado]
   ```
   Cada proteção também pode ser registrada sozinha: `UseHeadersSeguranca()`, `UseProtecaoSqlInjection()`, `UseOcultarErros()`, `UseRateLimit()`, `UseValidacaoUpload()`.

   O ocultar erros fica desligado em Development, e o HSTS não é enviado em Development.

3. (Opcional) Ajustar o rate limit no `appsettings.json`. Sem a seção, valem os padrões (100/min geral, 5/min em `/autenticacao`):
   ```json
   "Seguranca": {
     "RateLimit": {
       "Modo": "Bloquear",
       "LimitePorMinuto": 100,
       "Regras": [ { "Path": "/autenticacao", "LimitePorMinuto": 5 } ],
       "IpsLiberados": [],
       "RedesConfiaveis": [ "10.0.0.0/8", "132.2.8.9/23" ]
     }
   }
   ```
   `RedesConfiaveis` são as redes (CIDR) dos proxies reversos, como o Traefik: só quando a conexão vem delas o IP do cliente é lido do `X-Forwarded-For`. Sem valor, valem as redes privadas acima.

4. (Opcional) Ajustar os headers de segurança no `appsettings.json`. Sem a seção, valem os padrões abaixo; valor `""` desliga o header:
   ```json
   "Seguranca": {
     "HeadersSeguranca": {
       "XContentTypeOptions": "nosniff",
       "XFrameOptions": "DENY",
       "ReferrerPolicy": "no-referrer",
       "ContentSecurityPolicy": "default-src 'none'; frame-ancestors 'none'",
       "StrictTransportSecurity": "max-age=31536000",
       "PermissionsPolicy": "camera=(), microphone=(), geolocation=(), payment=()",
       "RemoverHeadersServidor": true
     }
   }
   ```
   O `ContentSecurityPolicy` padrão serve para APIs que só devolvem JSON. Se a API serve páginas HTML em produção (ex.: Swagger UI), use uma política mais permissiva ou `""`.

   O header `Server` do Kestrel é escrito depois do middleware; para removê-lo, desligue-o na inicialização:
   ```csharp
   // ASP.NET Core 2.2 a 5 (Program.cs com WebHost/Host builder)
   webBuilder.UseKestrel(o => o.AddServerHeader = false);

   // ASP.NET Core 6+ (Program.cs com WebApplication)
   builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);
   ```

## Como usar num projeto ASP.NET (.NET Framework)

1. Instalar o pacote `ProtecaoSeguranca` pelo NuGet do Visual Studio (feed RepoSTI). Funciona com `packages.config` e com `PackageReference`.

2. Registrar no `web.config` (pipeline integrado do IIS). Com o módulo único, as proteções seguem a mesma ordem e os mesmos padrões do `UsePortalSeguranca`:
   ```xml
   <system.webServer>
     <modules>
       <add name="PortalSeguranca" type="ProtecaoSeguranca.AspNet.PortalSegurancaModule, ProtecaoSeguranca" />
     </modules>
   </system.webServer>
   ```
   E ligar/desligar proteções no `appSettings`:
   ```xml
   <appSettings>
     <add key="Seguranca:UsarRateLimit" value="true" />          <!-- padrão: false -->
     <add key="Seguranca:UsarHeadersSeguranca" value="true" />   <!-- padrão: true -->
     <add key="Seguranca:UsarValidacaoUpload" value="true" />    <!-- padrão: false -->
   </appSettings>
   ```
   Ou, para escolher proteção por proteção, registrar só os módulos desejados, nesta ordem:
   ```xml
   <add name="HeadersSeguranca" type="ProtecaoSeguranca.AspNet.HeadersSegurancaModule, ProtecaoSeguranca" />
   <add name="OcultarErros" type="ProtecaoSeguranca.AspNet.OcultarErrosModule, ProtecaoSeguranca" />
   <add name="RateLimit" type="ProtecaoSeguranca.AspNet.RateLimitModule, ProtecaoSeguranca" />
   <add name="UploadArquivos" type="ProtecaoSeguranca.AspNet.UploadArquivosModule, ProtecaoSeguranca" />
   <add name="SqlInjection" type="ProtecaoSeguranca.AspNet.SqlInjectionModule, ProtecaoSeguranca" />
   ```

3. (Opcional) Ajustar valores no `appSettings`, com as mesmas chaves do `appsettings.json` do ASP.NET Core. Listas são separadas por vírgula e as regras do rate limit usam o formato `path=limite`:
   ```xml
   <appSettings>
     <add key="Seguranca:SqlInjection:Modo" value="Bloquear" />
     <add key="Seguranca:RateLimit:Modo" value="Bloquear" />
     <add key="Seguranca:RateLimit:LimitePorMinuto" value="100" />
     <add key="Seguranca:RateLimit:Regras" value="/autenticacao=5,/login=10" />
     <add key="Seguranca:RateLimit:IpsLiberados" value="10.1.2.3" />
     <add key="Seguranca:RateLimit:RedesConfiaveis" value="10.0.0.0/8,123.4.0.0/6,::1/179" />
     <add key="Seguranca:HeadersSeguranca:XFrameOptions" value="SAMEORIGIN" />
     <add key="Seguranca:HeadersSeguranca:ContentSecurityPolicy" value="" />
   </appSettings>
   ```

4. (Opcional) Enviar os logs para o log da aplicação. Por padrão vão para o `System.Diagnostics.Trace`; no `Application_Start` do `Global.asax`:
   ```csharp
   using ProtecaoSeguranca.AspNet;

   RegistroSeguranca.Destino = (nivel, mensagem, ex) => log.Warn(mensagem, ex);   // log4net, NLog, Serilog...
   ```

Diferenças em relação ao ASP.NET Core:
- **Ocultar erros** segue o `<customErrors>` do `web.config`: onde ele mostra detalhes (ex.: `RemoteOnly` acessando pelo próprio servidor), o erro original é mantido. Cobre exceções não tratadas de MVC, Web Forms e ASMX. O Web API trata as próprias exceções e já esconde os detalhes de clientes remotos por padrão (`IncludeErrorDetailPolicy.LocalOnly`).
- **HSTS** não é enviado em requisições locais (`localhost`).
- Alguns headers são adicionados pelo IIS/ASP.NET depois do módulo e precisam ser desligados no `web.config`:
  ```xml
  <system.web>
    <httpRuntime enableVersionHeader="false" />            <!-- X-AspNet-Version -->
  </system.web>
  <system.webServer>
    <httpProtocol>
      <customHeaders>
        <remove name="X-Powered-By" />
      </customHeaders>
    </httpProtocol>
  </system.webServer>
  ```
  No MVC, o `X-AspNetMvc-Version` sai com `MvcHandler.DisableMvcResponseHeader = true;` no `Application_Start`.

## Validação de upload

Protege contra o envio de arquivos maliciosos, como um script `.php` ou `.aspx` que depois é executado no servidor (web shell).
Vale para qualquer requisição `POST`/`PUT`/`PATCH` com `multipart/form-data`; as demais passam direto.

Cada arquivo passa por estas verificações, nesta ordem:
1. **Nome**: recusa nome vazio, sem extensão, com caracteres de controle ou com `:` (ex.: `shell.php::$DATA`). Pontos e espaços no fim são ignorados, como faz o Windows (`shell.php.` conta como `.php`).
2. **Extensões bloqueadas**: scripts de servidor (`.php`, `.aspx`, `.ashx`, `.jsp`, `.config`...), executáveis (`.exe`, `.dll`, `.bat`, `.ps1`, `.sh`...) e conteúdo ativo no navegador (`.html`, `.svg`, `.xml`...). Valem **em qualquer posição do nome**: `foto.php.jpg` é recusado. Sempre vencem a lista de permitidas.
3. **Extensões permitidas** (lista branca): por padrão `.pdf`, imagens (`.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`, `.tif`, `.tiff`, `.webp`), Office/LibreOffice (`.doc`, `.docx`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, `.odt`, `.ods`, `.odp`, `.rtf`), `.txt` e `.csv`.
4. **Tamanho** de cada arquivo (padrão 10 MB) e **quantidade** de arquivos por requisição (padrão 10).
5. **Assinatura**: o início do arquivo precisa bater com o formato da extensão (um `.pdf` começa com `%PDF`, um `.png` com `‰PNG`...). Também recusa executáveis Windows/Linux e scripts `#!` com qualquer extensão.
6. **Código embutido**: procura em todo o conteúdo trechos como `<?php`, `<script`, `<%@`, `runat="server"` (ex.: PHP escondido nos metadados de uma imagem). Em arquivos de texto (`.txt`, `.csv`) procura também os curtos, como `<?=` e `<%=`.

Arquivo recusado → **400** `{"mensagem":"Arquivo não permitido."}`; acima do tamanho → **413** `{"mensagem":"Arquivo excede o tamanho máximo permitido."}`. O motivo detalhado vai só para o log:
```
Upload de arquivo recusado, requisição bloqueada. IP: 200.x.x.x X-Forwarded-For:  POST /api/anexo Campo: arquivo Arquivo: foto.php.jpg Motivo: extensão bloqueada (.php)
```

### Ligar

- **ASP.NET Core:** `app.UsePortalSeguranca(o => o.UsarValidacaoUpload = true);` ou, sozinho, `app.UseValidacaoUpload();`.
- **.NET Framework:** `<add key="Seguranca:UsarValidacaoUpload" value="true" />` com o `PortalSegurancaModule`, ou o módulo `UploadArquivosModule` no `web.config`.

### Personalizar

Sem a seção, valem os padrões acima. Todos os campos são opcionais:
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
- `ExtensoesPermitidas`: substitui a lista padrão (com ou sem ponto). `"*"` aceita qualquer extensão que não esteja bloqueada — use só se não houver alternativa.
- `ExtensoesBloqueadas`: substitui a lista padrão. Para acrescentar algo, copie a lista `UploadArquivosOptions.ExtensoesBloqueadasPadrao` e inclua; evite remover itens.
- `Regras`: restringem endpoints específicos pelo trecho do path (a primeira que casar vale). Campos não informados na regra usam os gerais; as extensões bloqueadas valem sempre.
- `Modo: "SomenteLog"`: só registra, sem bloquear — útil para conferir por alguns dias se algum upload legítimo seria recusado.

No .NET Framework, as mesmas chaves vão no `appSettings`. As regras usam o formato `path=extensões|tamanho`:
```xml
<add key="Seguranca:UploadArquivos:ExtensoesPermitidas" value=".pdf,.jpg,.jpeg,.png,.docx,.xlsx" />
<add key="Seguranca:UploadArquivos:TamanhoMaximoMb" value="10" />
<add key="Seguranca:UploadArquivos:Regras" value="/documentos=.pdf|5MB,/fotos=.jpg|.jpeg|.png" />
```

**Importante:**
- O tamanho máximo do servidor continua valendo e é avaliado antes: no IIS, `maxRequestLength`/`maxAllowedContentLength`; no Kestrel, `MaxRequestBodySize`. Ajuste-os para não muito acima do maior upload esperado.
- A validação **não substitui** os cuidados no endpoint: salvar os arquivos **fora** da pasta publicada do site (ou numa pasta sem permissão de execução), gerar o nome no servidor (ex.: `Guid`) em vez de usar o nome enviado, e nunca servir o arquivo com o `Content-Type` informado pelo cliente.
- Uploads lidos direto do corpo (sem `multipart/form-data`, ex.: base64 dentro de um JSON) não passam por esta validação.

## Criptografia de dados

Permite entregar ao front dados que ele não consegue ler nem alterar (ids, CPF...) e recebê-los de volta já descriptografados na API.
Usos principais: impedir que o usuário troque um id na URL para acessar o registro de outra pessoa, e não expor dados pessoais no navegador.
Funciona no **ASP.NET Core (2.2 a 10)** e no **Web API 2 (.NET Framework)**, com o mesmo formato: um valor criptografado numa API Core é lido numa API Framework com a mesma chave.

### 1. Configurar a chave

Gere uma chave (uma vez) com `Criptografia.GerarChave()`, ou no PowerShell:
```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { [byte](Get-Random -Max 256) }))
```
Todas as instâncias e APIs que trocam dados criptografados entre si precisam da **mesma** chave. Guarde-a como segredo (variável de ambiente, cofre), não no repositório.

ASP.NET Core (`appsettings.json` ou variável de ambiente `Seguranca__Criptografia__Chave`):
```json
"Seguranca": {
  "Criptografia": {
    "Chave": "<32 bytes em base64>",
    "ChavesAnteriores": []
  }
}
```
.NET Framework (`web.config`):
```xml
<add key="Seguranca:Criptografia:Chave" value="&lt;32 bytes em base64&gt;" />
<add key="Seguranca:Criptografia:ChavesAnteriores" value="" />
```
**Troca de chave:** coloque a chave nova em `Chave` e mova a antiga para `ChavesAnteriores`. Valores novos saem com a chave nova e os já entregues ao front continuam válidos. Depois de um tempo, remova a antiga.

### 2. Ligar

- **ASP.NET Core:** nada a fazer. O `app.UsePortalSeguranca()` já liga a criptografia em todos os endpoints MVC (`o.UsarCriptografia = false` desliga). Se a API registra as proteções uma a uma, use `app.UseCriptografia()`.
- **Web API 2 (.NET Framework):** uma linha no `WebApiConfig.Register`:
  ```csharp
  using ProtecaoSeguranca.AspNet;

  config.UsarCriptografia();
  ```

Sem nenhum `[Criptografado]` nos DTOs, a criptografia não faz nada e não exige chave.

### 3. Marcar as propriedades

O atributo vai **só nos objetos**, não nos endpoints. Em **qualquer** endpoint, propriedades `[Criptografado]` saem criptografadas na resposta e são descriptografadas quando chegam no corpo da requisição:

```csharp
using ProtecaoSeguranca.Atributos;   // o mesmo no ASP.NET Core e no Web API 2

public class ServidorDto
{
    [Criptografado] public int Id { get; set; }      // vai e volta criptografado
    public string Nome { get; set; }                  // o front lê normalmente
    [Criptografado] public string Cpf { get; set; }
}

[HttpGet]
public ServidorDto Get() { ... }                      // o front recebe Id e Cpf criptografados

[HttpPut]
public IActionResult Put(ServidorDto dto) { ... }     // dto.Id chega como 123
```
O que o front recebe:
```json
{ "id": "ProtecaoSeguranca.AcpKJpjMZAIw5oaLf7...", "nome": "Maria", "cpf": "ProtecaoSeguranca.AcpKDzn0HUfCvVJ4..." }
```
Vale também para objetos internos e listas (`List<ServidorDto>`, `ServidorDto.Itens[].Codigo`...).

Quando o valor volta na **rota ou na query string** (ex.: `GET /servidor/ProtecaoSeguranca...`), marque o parâmetro:
```csharp
[HttpGet("servidor/{id}")]
public ServidorDto Get([Criptografado] int id) { ... }   // id chega como 123
```

**Regras importantes:**
- Um campo marcado que chegue **sem criptografia** (ex.: `124`) ou **adulterado** é recusado com 400 (`{"mensagem":"Dado criptografado inválido."}`) e registrado no log. Isso impede que alguém contorne a proteção mandando o id em texto puro.
- `null`, `""` e `0` são aceitos sem criptografia, para cadastros novos que ainda não têm id.
- O mesmo valor gera um texto diferente a cada resposta. O front deve tratar o valor como **opaco**: guardar e devolver, nunca comparar ou montar.
- Os tipos dos DTOs continuam os mesmos (`int Id`); a troca acontece no JSON. Números voltam como números.
- Parâmetro de rota criptografado: use `{id}`, **não** `{id:int}`. A restrição de tipo é avaliada antes da descriptografia e recusaria o texto criptografado.
- **Não substitui a autorização**: um valor criptografado continua valendo para quem o tiver. A API ainda precisa conferir se o usuário pode acessar aquele registro.
- No .NET Framework, só Web API 2 (`ApiController`). MVC 5 (`Controller`), Web Forms e ASMX não são suportados.

### Uso direto, sem atributo

```csharp
var id = Criptografia.Criptografar(123);          // também string, long e Guid
var original = Criptografia.Descriptografar(id);  // "123"; lança FormatException se inválido
Criptografia.TentarDescriptografar(id, out var valor);
```
No ASP.NET Core, para usar antes de qualquer requisição com `[Criptografado]`, chame uma vez `Criptografia.Configurar(Configuration)` na inicialização.

## Como lançar uma nova versão

1. Alterar o código. A lógica comum aos dois mundos fica em `Comum/` e `Configuracao/`; os middlewares do ASP.NET Core em `Middleware/` e `Extensions/`; os módulos do .NET Framework em `AspNet/`; o atributo `[Criptografado]` em `Atributos/` e os filtros de criptografia em `CriptografiaDados/` (`AspNetCore/` para o Core, `WebApi/` para o Web API 2).
2. Incrementar `<Version>` no `.csproj` (correção → 1.4.1; recurso novo compatível → 1.5.0; mudança incompatível → 2.0.0) e atualizar `<PackageReleaseNotes>`.
3. Gerar o pacote (precisa do SDK .NET 10; compila todas as versões de uma vez):
   ```bash
   dotnet pack ProtecaoSeguranca/ProtecaoSeguranca.csproj -c Release -o nupkgs
   ```
4. Publicar no repositório *hosted* do Nexus (URL e API key com a STI):
   ```bash
   dotnet nuget push nupkgs/ProtecaoSeguranca.<versão>.nupkg -s <URL do nuget-hosted> -k <API key>
   ```
5. Atualizar a `Version` do `PackageReference` (ou o pacote pelo NuGet do Visual Studio) nos projetos que devem receber a mudança.
