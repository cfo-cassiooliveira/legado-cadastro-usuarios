# Frontend Angular: build de produção (AOT) servido por nginx sem privilégios de root.
# Versões fixadas por digest (OWASP A03): Node 24.22.2 / nginx 1.28.2. Atualize tag e digest juntos.
FROM node:24.22.2-alpine@sha256:a34ab6d7f6f4813f4d0c5469d74685604b962fbdbf5793a7bc7b4998ecb867c8 AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

FROM nginxinc/nginx-unprivileged:1.28.2-alpine@sha256:7377697a821c131a924a7105fafbe7414db4e9fcc77a6f08f776f33f141ec3f8
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY security-headers.conf /etc/nginx/security-headers.conf
COPY --from=build /app/dist/legado-frontend/browser /usr/share/nginx/html
EXPOSE 8080
