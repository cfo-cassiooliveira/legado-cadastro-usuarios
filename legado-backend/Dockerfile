# API do legado. Contexto de build: raiz do repositório (Directory.*.props compartilhados).
# Versões fixadas por digest (OWASP A03): SDK 10.0.403 / ASP.NET 10.0.14. Atualize tag e digest juntos.
FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY legado-backend/src/ legado-backend/src/
RUN dotnet publish legado-backend/src/Legado.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14@sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c
WORKDIR /app
COPY --from=build /app .
# Usuário não-root fornecido pela imagem oficial.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Legado.Api.dll"]
