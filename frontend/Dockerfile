# Static build served by nginx; /api and /health are proxied to the API container (see nginx.conf).
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
# API_UPSTREAM is substituted into the template at start-up by the nginx image's entrypoint.
ENV API_UPSTREAM=http://api:8080
EXPOSE 80
HEALTHCHECK CMD wget -qO- http://127.0.0.1/healthz >/dev/null || exit 1
