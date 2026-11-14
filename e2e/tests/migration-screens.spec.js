// E2E do login de conta já migrada para o Keycloak (docker-compose.identity.yml): aviso e redirecionamento ao Cadastro.
// Exige E2E_MIGRATED_LOGIN e E2E_MIGRATED_PASSWORD de uma conta já migrada; sem eles o teste é pulado.
const { test, expect } = require("@playwright/test");
const AxeBuilder = require("@axe-core/playwright").default;

test("conta migrada com a senha certa vê o aviso e é levada ao Cadastro", async ({ page }) => {
  const login = process.env.E2E_MIGRATED_LOGIN;
  const password = process.env.E2E_MIGRATED_PASSWORD;
  test.skip(!login || !password, "defina E2E_MIGRATED_LOGIN e E2E_MIGRATED_PASSWORD (conta já migrada)");

  await page.goto("/login");
  await page.getByLabel("Login").fill(login);
  await page.getByLabel("Senha", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Entrar" }).click();

  await expect(page.getByRole("alert")).toContainText("Sua conta foi transferida para o novo sistema.");
  const link = page.getByRole("link", { name: "Ir para o novo sistema" });
  await expect(link).toHaveAttribute("href", /^https?:\/\//);
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"]).analyze();
  expect(results.violations.filter((v) => ["serious", "critical"].includes(v.impact)).map((v) => v.id)).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);

  // O Cadastro não está ao alcance do container do E2E: basta confirmar que o navegador sai para o endereço dele.
  const target = await link.getAttribute("href");
  const leaving = page.waitForRequest((request) => request.isNavigationRequest() && request.url() === target, { timeout: 8000 });
  await page.route(target, (route) => route.fulfill({ status: 200, contentType: "text/html", body: "<title>Cadastro</title>" }));
  await leaving;
});

test("\"Ficar nesta página\" cancela o redirecionamento da conta migrada", async ({ page }) => {
  const login = process.env.E2E_MIGRATED_LOGIN;
  const password = process.env.E2E_MIGRATED_PASSWORD;
  test.skip(!login || !password, "defina E2E_MIGRATED_LOGIN e E2E_MIGRATED_PASSWORD (conta já migrada)");

  await page.goto("/login");
  await page.getByLabel("Login").fill(login);
  await page.getByLabel("Senha", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Entrar" }).click();
  await page.getByRole("button", { name: "Ficar nesta página" }).click();

  await page.waitForTimeout(6000);
  await expect(page).toHaveURL(/\/login/);
  await expect(page.getByRole("link", { name: "Ir para o novo sistema" })).toBeVisible();
});
