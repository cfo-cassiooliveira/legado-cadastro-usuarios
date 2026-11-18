# Referência — Logs, LGPD e pseudonimização (movido do CLAUDE.md, verbatim)

- A proibição acima não tem exceção implícita (Constituição, Art. IV.6). Só um dado pessoal **aprovado em ADR**
  vai ao log, e apenas como campo estruturado com o nome do tipo: `{Cpf}`, `{Cnpj}`, `{Phone}`/`{MobilePhone}`,
  `{UserName}`, `{Email}` no message template, **nunca** concatenado ou interpolado (destruturado `{@Obj}` é
  mascarado; o token vai no topo como `Obj_Campo_token`, fora da consulta padrão do Cadastro). A API **pseudonimiza na origem** (ADR-0003 do `../graylog`,
  `Common/Pseudonymization/`): o `PseudonymizationEnricher` troca o campo pela máscara do ADR-0002 (`***.456.789-**`,
  `11*****4321`, `mar***`, `ana***@gmail`) e soma `<Campo>_token` (`k1-` + HMAC-SHA256 truncado; e-mail e login dão
  o mesmo token), em qualquer nível (objeto, dicionário, lista); fora do formato vira `***`, sem token. O texto
  renderizado sai só com a máscara, nunca com o token. No `Scope` dos escopos só o valor com máscara válida é trocado pela
  máscara, e CPF/CNPJ/telefone fora do formato com 8+ dígitos vira `***`; login/e-mail curto ou inválido não é trocado
  (o Graylog o troca por `***`): nunca use dado pessoal em
  template de escopo. A exceção não é alterada, então nunca leve dado pessoal nela. O Graylog é só a rede de segurança (ADR-0002 do
  `../graylog`). Os nomes ficam em `PersonalDataFields`, iguais às listas `LGPD_*` do
  `../graylog/provisioning/provision.sh`: nome novo entra nos dois. Regras que **não podem regredir**: vetores do
  `plan.md` da spec 001 do graylog e nenhum valor em claro no GELF (`Logging/PseudonymizationTests.cs`,
  `Logging/PseudonymizedGelfTests.cs`); sem chave `Logging:Pseudonymization:Keys:<CurrentKeyId>` válida a API não
  sobe (chave vazia = ausente). Chave só em `.env`/user-secrets (`scripts/log-pseudonymization-key.sh [kN]`), a mesma
  do `../cadastro`; o compose mapeia k1 e k2 (rotação: k2 + `LOG_PSEUDONYMIZATION_CURRENT_KEY_ID=k2`, depois a K1 sai).
- Níveis (regra em `observability.md`): exceção capturada = Error; falha de negócio ou falha prevista de banco
  (unicidade/concorrência) = Warning + resultado tratado; sucesso = Information. Frontend usa `AppLogger`
  (`POST /api/v1/client-logs`) e o `GlobalErrorHandler`; todo catch passa pelo logger. O endpoint tira query string e
  fragmento da origem e mascara pelo formato (`PersonalDataTextMasker`, máscara do `PersonalDataMasker`, sem token)
  e-mail, CNPJ, CPF e celular com DDD em `ClientMessage`/`ClientSource`/`ClientStack`; nunca envie valor de formulário.
- Serilog com `preserveStaticLogger: true` e request logging com o logger do host: nunca dependa do `Log.Logger`
  estático (um host descartado o encerrava e silenciava os logs dos outros).

