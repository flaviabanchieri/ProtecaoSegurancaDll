# Proteção contra SQL Injection

Bloqueia requisições que trazem padrões de SQL Injection antes que cheguem ao controller.
É uma camada a mais: **não substitui** consultas parametrizadas (Dapper, Entity Framework, `SqlParameter`) no acesso a dados.

## Disponibilidade

| Plataforma | Como ligar | Padrão |
|---|---|---|
| ASP.NET Core | `app.UsePortalSeguranca()` ou `app.UseProtecaoSqlInjection()` | sempre ligado no `UsePortalSeguranca` |
| .NET Framework | `PortalSegurancaModule` ou `SqlInjectionModule` no `web.config` | sempre ligado no `PortalSegurancaModule` |

Instalação: ver [Instalação e configuração](00-instalacao.md).

## O que é verificado

| Onde | Métodos | Observação |
|---|---|---|
| Path da URL | todos | `/api/servidor/1;drop table x` |
| Query string (nomes e valores) | todos | `?nome=1' or 1=1` |
| Corpo JSON (nomes e valores texto, em qualquer nível) | `POST`, `PUT`, `PATCH` | `Content-Type` contendo `json` |
| Corpo de formulário (nomes e valores) | `POST`, `PUT`, `PATCH` | `application/x-www-form-urlencoded` e `multipart/form-data` |

Não são verificados:
- Requisições `OPTIONS` (preflight de CORS).
- Corpos acima de **1 MB**.
- Campos chamados `password`, `senha` ou `token` (sem diferenciar maiúsculas), no corpo.
- Valores criptografados pela própria DLL (`ProtecaoSeguranca.…`), que podem conter sequências parecidas com SQL.
- Números, booleanos e `null` no JSON (só textos são analisados).

Antes da análise, o valor é decodificado de URL (até 3 vezes, contra `%2527` etc.) e `+` vira espaço.

## O que é considerado suspeito

- **Aspas simples**, inclusive as variantes Unicode (`'`, `’`, `‘`, `ʼ`, `ʹ`, `＇`).
- **Comentários SQL**: `--`, `/*`, `*/`.
- **Ponto e vírgula**:
  - no path e na query string, qualquer `;`;
  - no corpo (textos livres), só `;` seguido de comando (`; drop`, `; select`, `; exec`...).
- **Palavras-chave em sequência**, por exemplo:
  - `union select`, `select ... from`, `insert into`, `delete from`, `update x set`;
  - `drop table`, `alter database`, `truncate table`, `exec(`, `execute `, `declare @`;
  - `xp_…`, `sp_…`, `dbms_…`, `utl_…`, `@@version`, `information_schema`, `sys.…`;
  - `waitfor delay`, `sleep(`, `benchmark(`, `pg_sleep(`;
  - `char(`, `nchar(`, `chr(`, `cast(`, `convert(`;
  - `or 1=1`, `or x like y`, `and a=b`;
  - literais hexadecimais longos (`0x41424344…`).

Palavras soltas (ex.: “select” num texto) não bastam: é preciso o padrão completo.

## Resposta ao bloquear

```
HTTP/1.1 400 Bad Request
Content-Type: application/json; charset=utf-8

{"mensagem":"Requisição contém caracteres ou termos não permitidos."}
```

Log (o valor é truncado em 200 caracteres):
```
Possível SQL Injection, requisição bloqueada. IP: 200.x.x.x X-Forwarded-For:  GET /api/servidor Campo: nome Valor: 1' or 1=1
```

## Configuração

Só existe uma opção, o modo:

**ASP.NET Core** (`appsettings.json`):
```json
"Seguranca": {
  "SqlInjection": { "Modo": "SomenteLog" }
}
```

**.NET Framework** (`web.config`):
```xml
<add key="Seguranca:SqlInjection:Modo" value="SomenteLog" />
```

| Valor | Efeito |
|---|---|
| `Bloquear` (padrão) | Registra no log e responde 400. |
| `SomenteLog` | Só registra no log, com o prefixo `[MONITORAMENTO]`, e deixa a requisição seguir. |

Ver [Modo somente monitorar](07-modo-monitoramento.md).

## Falsos positivos

Textos legítimos que podem ser recusados:
- Nomes e sobrenomes com apóstrofo: `D'Ávila`, `O'Connor`, `Sant'Anna`.
- Textos livres com `--` (ex.: “Observação -- ver anexo”).
- Filtros na query string com `;`.

O que fazer:
1. Começar em `SomenteLog` e analisar os registros `[MONITORAMENTO]` antes de bloquear.
2. Para senhas e tokens, usar os nomes `password`, `senha` ou `token` no corpo (são ignorados).
3. Para textos livres com `;`, enviá-los no **corpo** e não na query string.
4. Se um sistema depende de apóstrofos e não é possível adaptar o front, avaliar com a equipe de segurança registrar as proteções individualmente sem o `UseProtecaoSqlInjection`/`SqlInjectionModule`.

## Particularidades do .NET Framework

- A query string e o formulário são lidos por `Request.Unvalidated`, para não disparar a validação de requisição do ASP.NET (`HttpRequestValidationException`) antes da hora.
- O IP no log é o `UserHostAddress` (o IP do proxy, se houver); o `X-Forwarded-For` vem logo em seguida.
