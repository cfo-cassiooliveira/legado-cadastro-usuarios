namespace Legado.Api.IntegrationTests.Security;

/// <summary>
/// Guarda a CSP do nginx (<c>legado-frontend/security-headers.conf</c>) contra regressão. O nginx não roda nos testes de
/// integração da API; como a CSP é só texto de configuração, a verificação lê o arquivo versionado. Sem isto, voltar
/// <c>'unsafe-inline'</c> ou perder o Trusted Types passaria sem nenhum teste falhar.
/// </summary>
public sealed class ContentSecurityPolicyTests
{
    private static readonly string FrontendDirectory = FindFrontendDirectory();

    [Theory]
    [InlineData("script-src")]
    [InlineData("style-src")]
    [InlineData("default-src")]
    public void Csp_Directive_NeverAllowsUnsafeInline(string directive)
    {
        var sources = Directive(directive);

        sources.ShouldNotContain("'unsafe-inline'");
        sources.ShouldNotContain("'unsafe-eval'");
    }

    [Fact]
    public void Csp_RequiresTrustedTypesForScripts()
    {
        var csp = ContentSecurityPolicy();

        csp.ShouldContain("require-trusted-types-for 'script'");
        csp.ShouldContain("trusted-types ");
    }

    // Todo "location" do nginx inclui o arquivo (add_header dentro de location anula os herdados) e nada redefine a CSP.
    [Fact]
    public void Nginx_IncludesTheSecurityHeadersInEveryLocationAndNeverRedefinesTheCsp()
    {
        var nginx = File.ReadAllText(Path.Combine(FrontendDirectory, "nginx.conf"));

        nginx.ShouldNotContain("Content-Security-Policy", Case.Insensitive);
        var locations = nginx.Split('\n').Count(line => line.TrimStart().StartsWith("location ", StringComparison.Ordinal));
        var includes = nginx.Split('\n').Count(line => line.Contains("include /etc/nginx/security-headers.conf;", StringComparison.Ordinal));
        includes.ShouldBeGreaterThanOrEqualTo(locations);
    }

    private static string ContentSecurityPolicy()
    {
        var line = File.ReadAllLines(Path.Combine(FrontendDirectory, "security-headers.conf"))
            .Single(l => l.TrimStart().StartsWith("add_header Content-Security-Policy ", StringComparison.Ordinal));
        var start = line.IndexOf('"', StringComparison.Ordinal) + 1;
        return line[start..line.LastIndexOf('"')];
    }

    // Fontes de uma diretiva; vazio quando a diretiva não existe (cai no default-src, também verificado).
    private static string Directive(string name) =>
        ContentSecurityPolicy().Split(';', StringSplitOptions.TrimEntries)
            .FirstOrDefault(d => d.StartsWith(name + " ", StringComparison.Ordinal)) ?? string.Empty;

    private static string FindFrontendDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "legado-frontend");
            if (File.Exists(Path.Combine(candidate, "security-headers.conf")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("legado-frontend/security-headers.conf não encontrado acima de " + AppContext.BaseDirectory);
    }
}
