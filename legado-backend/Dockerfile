# API do legado. Contexto de build: raiz do repositório (Directory.*.props compartilhados).
# Versões fixadas por digest (OWASP A03): SDK 10.0.403 / ASP.NET 10.0.14. Atualize tag e digest juntos.
FROM mcr.microsoft.com/dotnet/sdk:10.0.404@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY legado-backend/src/ legado-backend/src/
RUN dotnet publish legado-backend/src/Legado.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e
WORKDIR /app
COPY --from=build /app .
# Usuário não-root fornecido pela imagem oficial.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Legado.Api.dll"]
