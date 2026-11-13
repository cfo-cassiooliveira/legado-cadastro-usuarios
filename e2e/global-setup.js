// Dentro do container do Playwright, "localhost:4200" é o próprio container. Quando E2E_FORWARD_TO está definido
// (scripts/e2e.sh), este setup encaminha localhost:4200 para o nginx publicado no host, preservando o hostname
// "localhost", o único tratado como contexto seguro em HTTP (exigido pelo cookie __Host-legado-fgp).
const net = require("node:net");

module.exports = async () => {
  const target = process.env.E2E_FORWARD_TO;
  if (!target) {
    return undefined;
  }
  const [targetHost, targetPort] = target.split(":");
  const listenPort = Number(new URL(process.env.E2E_BASE_URL || "http://localhost:4200").port);

  const server = net.createServer((client) => {
    const upstream = net.connect(Number(targetPort), targetHost);
    client.pipe(upstream).pipe(client);
    // Conexão encerrada de um lado fecha a outra; erros de rede só derrubam o par afetado.
    client.on("error", () => upstream.destroy());
    upstream.on("error", () => client.destroy());
  });
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(listenPort, "127.0.0.1", resolve);
  });

  return async () => new Promise((resolve) => server.close(resolve));
};
