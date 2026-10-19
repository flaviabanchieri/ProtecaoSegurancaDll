# Criptografia de dados (`[Criptografado]`)

Permite entregar ao front dados que ele não consegue ler nem alterar (ids, CPF...) e recebê-los de volta já descriptografados na API.

Usos principais:
- impedir que o usuário troque um id na URL ou no corpo para acessar o registro de outra pessoa;
- não expor dados pessoais no navegador (DevTools, cache, logs do front).

## Disponibilidade

| Plataforma | Como ligar | Padrão |
|---|---|---|
| ASP.NET Core 2.2 a 10 | `app.UsePortalSeguranca()` ou `app.UseCriptografia()` | ligado (`o.UsarCriptografia = false` desliga) |
| Web API 2 (.NET Framework) | `config.UsarCriptografia()` no `WebApiConfig` | desligado até chamar |
| MVC 5, Web Forms, ASMX | não suportado | — |

O formato é o mesmo em todas as plataformas: um valor criptografado numa API ASP.NET Core é lido numa API .NET Framework que use a mesma chave.

Sem nenhum `[Criptografado]` nos DTOs, a criptografia não faz nada e não exige chave.

## Passo 1 — Gerar e configurar a chave

Gere uma chave **uma vez** e use-a em todas as instâncias e APIs que trocam dados criptografados entre si.

Em C#:
```csharp
var chave = ProtecaoSeguranca.Criptografia.GerarChave();   // 32 bytes aleatórios em base64
```
Ou no PowerShell:
```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { [byte](Get-Random -Max 256) }))
```

Guarde a chave como **segredo** (variável de ambiente, cofre de segredos, configuração do servidor), **nunca no repositório**.

**ASP.NET Core** — `appsettings.json` (em produção, prefira a variável de ambiente `Seguranca__Criptografia__Chave`):
```json
"Seguranca": {
  "Criptografia": {
    "Chave": "<32 bytes em base64>",
    "ChavesAnteriores": []
  }
}
```

**.NET Framework** — `web.config`:
```xml
<appSettings>
  <add key="Seguranca:Criptografia:Chave" value="&lt;32 bytes em base64&gt;" />
  <add key="Seguranca:Criptografia:ChavesAnteriores" value="" />
</appSettings>
```

### Troca de chave

1. Gere uma chave nova.
2. Coloque a nova em `Chave` e mova a antiga para `ChavesAnteriores`.
3. Publique. Valores novos saem com a chave nova; os que já estão no front continuam válidos.
4. Depois de um tempo (ex.: quando nenhuma sessão antiga estiver aberta), remova a antiga.

## Passo 2 — Ligar

**ASP.NET Core:** nada a fazer além do `app.UsePortalSeguranca()`. Se a API registra as proteções uma a uma, chame `app.UseCriptografia()`.

**Web API 2:** uma linha no `WebApiConfig.Register`:
```csharp
using ProtecaoSeguranca.AspNet;

public static void Register(HttpConfiguration config)
{
    config.UsarCriptografia();
    // ... rotas, formatadores etc.
}
```

## Passo 3 — Marcar as propriedades

O atributo vai **só nos DTOs**, não nos endpoints. Em qualquer endpoint, propriedades `[Criptografado]`:
- **saem criptografadas** na resposta;
- **são descriptografadas** quando chegam no corpo da requisição.

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

### Valores na rota ou na query string

Quando o valor volta na URL (ex.: `GET /servidor/ProtecaoSeguranca...`), marque o **parâmetro**:
```csharp
[HttpGet("servidor/{id}")]
public ServidorDto Get([Criptografado] int id) { ... }   // id chega como 123
```
Use `{id}` e **não** `{id:int}`: a restrição de tipo da rota é avaliada antes da descriptografia e recusaria o texto criptografado.

### Tipos suportados

- **Propriedades de DTO:** qualquer tipo (`string`, `int`, `long`, `Guid`, `decimal`, datas, anuláveis e até objetos ou listas inteiras). O valor é criptografado como JSON e volta com o mesmo tipo: números voltam como números, objetos como objetos.
- **Parâmetros de rota/query:** tipos simples (`int`, `long`, `string`, `Guid`).

Os tipos dos DTOs continuam os mesmos (`int Id`); a troca acontece só no JSON trafegado.

## Regras importantes

- **Valor sem criptografia ou adulterado é recusado.** Um campo marcado que chegue como `124` (em vez de `ProtecaoSeguranca...`) ou com o texto alterado gera:
  ```
  HTTP/1.1 400 Bad Request
  {"mensagem":"Dado criptografado inválido."}
  ```
  e um registro no log. Isso impede que alguém contorne a proteção mandando o id em texto puro.
- **`null`, `""` e `0` são aceitos sem criptografia**, para cadastros novos que ainda não têm id.
- **O mesmo valor gera um texto diferente a cada resposta.** O front deve tratar o valor como **opaco**: guardar e devolver, nunca comparar dois ids criptografados, ordenar por eles ou montá-los.
- **Não substitui a autorização.** Um valor criptografado continua válido para quem o tiver (ex.: se um usuário repassar o link). A API ainda precisa conferir se o usuário logado pode acessar aquele registro.
- O algoritmo é AES-256-CBC com HMAC-SHA256 (o valor não pode ser lido nem alterado sem a chave). Os valores começam com `ProtecaoSeguranca.`.

## Uso direto, sem atributo

Para criptografar fora dos DTOs (ex.: montar um link enviado por e-mail):
```csharp
using ProtecaoSeguranca;

var id = Criptografia.Criptografar(123);          // também string, long e Guid
var original = Criptografia.Descriptografar(id);  // "123"; lança FormatException se inválido

if (Criptografia.TentarDescriptografar(id, out var valor))
{
    // valor == "123"
}
```

No **ASP.NET Core**, a chave é carregada sozinha na primeira requisição que usa `[Criptografado]`. Para usar `Criptografar`/`Descriptografar` antes disso (ex.: num job ou na inicialização), chame uma vez:
```csharp
Criptografia.Configurar(builder.Configuration);   // ou Configuration, no Startup
```
No **.NET Framework**, a chave é lida do `web.config` na primeira chamada; nada a fazer.

## Problemas comuns

| Sintoma | Causa |
|---|---|
| `InvalidOperationException: Criptografia sem chave configurada` | Há `[Criptografado]` em uso, mas falta `Seguranca:Criptografia:Chave`. |
| `InvalidOperationException: Criptografia não configurada` | `Criptografia.Criptografar` chamado no ASP.NET Core antes de qualquer requisição; chame `Criptografia.Configurar(Configuration)` na inicialização. |
| 400 `Dado criptografado inválido` depois de publicar | A chave mudou e a antiga não foi colocada em `ChavesAnteriores`, ou as instâncias estão com chaves diferentes. |
| 404 na rota com id criptografado | A rota usa `{id:int}`; troque para `{id}`. |
| Valor chega em texto puro no front | O DTO está em MVC 5/Web Forms (não suportado), ou `UsarCriptografia` não foi chamado no Web API 2. |
