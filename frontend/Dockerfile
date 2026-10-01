# syntax=docker/dockerfile:1
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
# The official image renders /etc/nginx/templates/*.template with envsubst at startup.
ENV API_UPSTREAM=http://host.docker.internal:5080
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist/hr-service-desk/browser /usr/share/nginx/html
EXPOSE 8080
