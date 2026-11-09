# Migrator do banco legado. Contexto de build: raiz do repositório (precisa do backend para o modelo EF).

# Etapa 1: gera o EF migration bundle (executável self-contained para Linux).
# Versões fixadas por digest (OWASP A03): SDK 10.0.403 / ASP.NET 10.0.14. Atualize tag e digest juntos.
FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
WORKDIR /src
COPY dotnet-tools.json Directory.Build.props Directory.Packages.props .editorconfig ./
RUN dotnet tool restore
COPY legado-backend/src/ legado-backend/src/
COPY legado-database/src/ legado-database/src/
# O bundle não restaura sozinho para o runtime alvo.
RUN dotnet restore legado-database/src/Legado.Database -r linux-x64
RUN dotnet ef migrations bundle \
      --project legado-database/src/Legado.Database \
      --startup-project legado-database/src/Legado.Database \
      --self-contained -r linux-x64 --configuration Release \
      --output /out/efbundle

# Etapa 2: reaproveita a imagem do SQL Server apenas pelo sqlcmd (mesma imagem do banco, já baixada).
FROM mcr.microsoft.com/mssql/server:2025-latest@sha256:245f6b1c8903edae52cb20fa31419cefcde1a49938bb8c3447099011fc871ca3
WORKDIR /migrator
# O bundle é single-file e extrai dependências; o home do usuário mssql não é gravável.
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/efbundle
COPY --from=build --chmod=755 /out/efbundle ./efbundle
COPY --chmod=644 legado-database/scripts/create-app-login.sql ./
COPY --chmod=755 legado-database/scripts/migrate.sh ./
ENTRYPOINT ["/migrator/migrate.sh"]
