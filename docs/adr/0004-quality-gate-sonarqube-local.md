# ADR-0004: Quality Gate no SonarQube local (script único para análise local e CI)

- **Status**: Aceito
- **Data**: 2026-11-09
- **Decisores**: Cassio Oliveira (responsável técnico)
- **Artigos da Constituição relacionados**: Art. V.2 e V.3 (Quality Gate do SonarQube)

## Contexto

O CI já tinha o job `quality-gate`, mas o pulava porque não havia servidor SonarQube. O projeto
`C:\Workspace\sonarqube` passou a oferecer um SonarQube Community Build provisionado em Docker
(ver o ADR-0001 daquele projeto). Como os runners do GitHub Actions rodam na nuvem, eles não alcançam um servidor
publicado só em `127.0.0.1`.

## Decisão

1. A análise roda pelo script `scripts/sonar-analysis.sh`, **fonte única** dos parâmetros (key, cobertura, exclusões).
   O job `quality-gate` do CI chama o mesmo script, o que evita divergência entre a análise local e a do CI.
2. Localmente, o script usa `http://localhost:9001` e o token por projeto gerado pelo provisionamento do SonarQube
   (`../sonarqube/.tokens/legado-cadastro-usuarios.token`, fora do git). No CI, usa os segredos do repositório.
3. O job do CI continua **opcional** enquanto não houver um servidor acessível pela internet. Com os segredos
   definidos, ele volta a ser obrigatório e bloqueia o build das imagens se o gate reprovar.
4. Antes de abrir um PR, o desenvolvedor roda o script e só segue com o Quality Gate aprovado.

## Alternativas consideradas

| Alternativa | Prós | Contras | Motivo da rejeição |
|---|---|---|---|
| Runner self-hosted nesta máquina | Gate automático no PR | Mais um serviço para manter, com acesso ao repositório | Não necessário no momento |
| Parâmetros duplicados no CI e no script | Nenhuma mudança no CI | Os dois divergem com o tempo | DRY por conceito (Art. II.4) |

## Consequências

- **Positivas**: uma única forma de analisar, igual no local e no CI. O gate passa a ser medido de verdade.
- **Negativas / riscos**: sem servidor público, o gate no PR depende de disciplina (rodar o script). O Community Build
  não analisa branches nem PRs: a análise local sobrescreve a da branch principal.
- **Ações decorrentes**: quando houver um SonarQube acessível pelo CI (edição paga ou SonarQube Cloud), definir
  `SONAR_TOKEN`/`SONAR_HOST_URL` e habilitar a análise de PR.
