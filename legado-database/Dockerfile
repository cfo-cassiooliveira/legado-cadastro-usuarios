# Migrator do banco legado. Contexto de build: raiz do repositório (precisa do backend para o modelo EF).

# Etapa 1: gera o EF migration bundle (executável self-contained para Linux).
# Versões fixadas por digest (OWASP A03): SDK 10.0.403 / ASP.NET 10.0.14. Atualize tag e digest juntos.
FROM mcr.microsoft.com/dotnet/sdk:10.0.404@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
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
FROM mcr.microsoft.com/mssql/server:2025-latest@sha256:05644dad1266ca3d445d1e8f6d2ebb9a6a03df9a39376e0f7b48917356dca9d9
WORKDIR /migrator
# O bundle é single-file e extrai dependências; o home do usuário mssql não é gravável.
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/efbundle
COPY --from=build --chmod=755 /out/efbundle ./efbundle
COPY --chmod=644 legado-database/scripts/create-app-login.sql ./
COPY --chmod=755 legado-database/scripts/migrate.sh ./
# A imagem do SQL Server já roda como mssql; declarado aqui para não depender do herdado (Trivy DS002).
USER mssql
ENTRYPOINT ["/migrator/migrate.sh"]
