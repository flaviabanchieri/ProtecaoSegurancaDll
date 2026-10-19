# Ocultar erros internos (5xx)

Impede que detalhes de erros internos — stack trace, mensagens do banco, nomes de tabelas — cheguem ao cliente.
O cliente recebe uma mensagem genérica com um **código de correlação**; o erro real vai para o log com o mesmo código, para que o suporte encontre o registro.

## Disponibilidade

| Plataforma | Como ligar | Padrão |
|---|---|---|
| ASP.NET Core | `app.UsePortalSeguranca()` ou `app.UseOcultarErros()` | sempre ligado no `UsePortalSeguranca` |
| .NET Framework | `PortalSegurancaModule` ou `OcultarErrosModule` no `web.config` | sempre ligado no `PortalSegurancaModule` |

Instalação: ver [Instalação e configuração](00-instalacao.md). Não há configuração no `appsettings`.

## Como funciona no ASP.NET Core

Vale para:
- **Exceções não tratadas** que chegam até o middleware → resposta 500.
- **Respostas com status 500 ou maior** geradas pela própria API (ex.: `StatusCode(500, ex.Message)`).

O que o cliente recebe:
```
HTTP/1.1 500 Internal Server Error
Content-Type: application/json; charset=utf-8
X-Correlation-Id: 3f9a1c0b7d2e

{"mensagem":"Ocorreu um erro interno. Código: 3f9a1c0b7d2e"}
```

Se a API já devolvia um JSON próprio no erro, **a estrutura é mantida** (o envelope que o front espera) e só os textos são trocados:
```json
// resposta original da API
{ "sucesso": false, "erro": "Violation of PRIMARY KEY constraint 'PK_Servidor'...", "codigo": 2627 }

// o que o cliente recebe
{ "sucesso": false, "erro": "Ocorreu um erro interno. Código: 3f9a1c0b7d2e", "codigo": 2627 }
```
Números e booleanos são mantidos; todo valor texto vira a mensagem genérica. Se o corpo original não for JSON, o cliente recebe `{"mensagem":"..."}`.

O que vai para o log:
```
Erro interno 3f9a1c0b7d2e. POST /api/servidor?x=1 Status: 500 Resposta original: {"sucesso":false,"erro":"Violation of PRIMARY KEY..."}
```
ou, para exceções, a exceção completa com `Erro interno 3f9a1c0b7d2e. POST /api/servidor`.

Detalhes:
- **Desligado em `Development`**: em desenvolvimento a mensagem original aparece normalmente.
- Respostas abaixo de 500 (400, 404, 422...) passam sem alteração e sem buffer.
- Até 64 KB do corpo original são guardados para o log.
- Se a resposta já tinha começado a ser enviada quando a exceção ocorreu, não há como trocá-la: o erro é registrado e a exceção segue.

## Como funciona no .NET Framework

Vale para **exceções não tratadas** de MVC, Web Forms e ASMX (evento `Application_Error`).
A resposta é sempre `{"mensagem":"Ocorreu um erro interno. Código: …"}` com o header `X-Correlation-Id`, e o erro vai para o log configurado em `RegistroSeguranca` (ver [Instalação](00-instalacao.md#44-logs)).

Diferenças em relação ao ASP.NET Core:
- **Segue o `<customErrors>` do `web.config`**. Onde ele mostra detalhes, o erro original é mantido:
  ```xml
  <system.web>
    <customErrors mode="RemoteOnly" />   <!-- detalhes só acessando pelo próprio servidor -->
  </system.web>
  ```
  `On` sempre oculta; `Off` nunca oculta (não use `Off` em produção).
- Erros HTTP abaixo de 500 (ex.: 404 de `HttpException`) não são tratados.
- **Web API 2** trata as próprias exceções e não passa por este módulo. Ele já esconde os detalhes de clientes remotos por padrão (`IncludeErrorDetailPolicy.LocalOnly`); não mude para `Always` em produção.
- Respostas 5xx montadas pela própria aplicação (sem exceção) não são alteradas.

## Como usar o código de correlação

1. O usuário relata o erro informando o código mostrado na tela (ou o front lê o header `X-Correlation-Id`).
2. O suporte procura o código no log da aplicação e encontra a mensagem e a exceção originais.

Dica para o front: exibir a `mensagem` recebida, que já contém o código, em vez de uma mensagem fixa.
