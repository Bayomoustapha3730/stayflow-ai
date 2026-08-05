# syntax=docker/dockerfile:1.7

FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/package*.json ./
RUN npm ci
COPY frontend/. ./
ARG VITE_STAYFLOW_API_URL
ARG VITE_STAYFLOW_SIGNALR_URL
ARG VITE_STAYFLOW_ENVIRONMENT=production
ENV VITE_STAYFLOW_API_URL=$VITE_STAYFLOW_API_URL \
    VITE_STAYFLOW_SIGNALR_URL=$VITE_STAYFLOW_SIGNALR_URL \
    VITE_STAYFLOW_ENVIRONMENT=$VITE_STAYFLOW_ENVIRONMENT
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
WORKDIR /usr/share/nginx/html

COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist ./
COPY frontend/docker-entrypoint.d/30-runtime-config.sh /docker-entrypoint.d/30-runtime-config.sh
RUN chmod +x /docker-entrypoint.d/30-runtime-config.sh

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s CMD wget -qO- http://127.0.0.1:8080/healthz || exit 1
CMD ["nginx", "-g", "daemon off;"]
