// E2E das telas públicas (login e cadastro): acessibilidade, contraste, responsividade e fluxo de erro do login.
const { test, expect } = require("@playwright/test");
const AxeBuilder = require("@axe-core/playwright").default;

const SCREENS = [
  { name: "login", path: "/login", heading: "Entrar" },
  { name: "cadastro", path: "/register", heading: "Cadastro de usuário" },
];

// Razão de contraste WCAG entre a borda de um campo e o fundo efetivo atrás dele (primeiro ancestral opaco).
const fieldBorderContrast = (page, selector) =>
  page.evaluate((sel) => {
    const parse = (c) => c.match(/[\d.]+/g).map(Number);
    const lum = ([r, g, b]) =>
      [r, g, b]
        .map((v) => {
          const s = v / 255;
          return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
        })
        .reduce((sum, v, i) => sum + v * [0.2126, 0.7152, 0.0722][i], 0);
    const field = document.querySelector(sel);
    let behind = field.parentElement;
    while (behind && parse(getComputedStyle(behind).backgroundColor)[3] === 0) behind = behind.parentElement;
    const [a, b] = [lum(parse(getComputedStyle(field).borderTopColor)), lum(parse(getComputedStyle(behind ?? document.body).backgroundColor))];
    return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
  }, selector);

for (const screen of SCREENS) {
  test.describe(`tela de ${screen.name}`, () => {
    test.beforeEach(async ({ page }) => {
      await page.goto(screen.path);
      await expect(page.getByRole("heading", { level: 1 })).toHaveText(screen.heading);
    });

    // Regressão: o painel lateral também tinha um h1; o idioma saía "pt" e a área interna podia ser indexada.
    test("tem um único h1, idioma pt-BR e noindex", async ({ page }) => {
      await expect(page.locator("h1")).toHaveCount(1);
      await expect(page.locator("html")).toHaveAttribute("lang", "pt-BR");
      await expect(page.locator('meta[name="robots"]')).toHaveAttribute("content", /noindex/);
    });

    test("não tem violação séria ou crítica do axe", async ({ page }) => {
      const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"]).analyze();
      const blocking = results.violations
        .filter((v) => ["serious", "critical"].includes(v.impact))
        .map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`);

      expect(blocking).toEqual([]);
    });

    // Regressão: borda dos campos com 1,48:1 (WCAG 1.4.11 exige 3:1) e fonte de 15,2px (o iOS dá zoom abaixo de 16px).
    test("campos com borda visível e fonte de pelo menos 16px", async ({ page }) => {
      const field = page.locator("input:not([type=hidden])").first();
      const selector = `#${await field.getAttribute("id")}`;

      expect(await fieldBorderContrast(page, selector)).toBeGreaterThanOrEqual(3);
      expect(parseFloat(await field.evaluate((el) => getComputedStyle(el).fontSize))).toBeGreaterThanOrEqual(16);
    });

    test("não gera rolagem horizontal", async ({ page }) => {
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);

      expect(overflow).toBe(false);
    });
  });
}

test("login com credenciais inválidas mostra a mensagem genérica junto do formulário", async ({ page }) => {
  await page.goto("/login");

  await page.getByLabel("Login").fill("e2e.inexistente");
  await page.getByLabel("Senha", { exact: true }).fill("Senha@Errada123");
  await page.getByRole("button", { name: "Entrar" }).click();

  await expect(page.getByRole("alert")).toContainText("Login ou senha inválidos.");
  await expect(page).toHaveURL(/\/login/);
});

// Cada tela carrega por completo antes da próxima: navegar no meio do carregamento aborta o import dos chunks
// e o próprio envio do log (o WebKit acusa isso no console), o que não é o uso normal da tela.
for (const screen of SCREENS) {
  test(`tela de ${screen.name} carrega sem erros no console`, async ({ page }) => {
    const errors = [];
    page.on("console", (message) => {
      if (message.type() === "error") errors.push(message.text());
    });
    page.on("pageerror", (error) => errors.push(error.message));

    await page.goto(screen.path, { waitUntil: "networkidle" });

    expect(errors).toEqual([]);
  });
}
