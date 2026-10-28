# Frontend: build Angular, poi nginx che serve i file e inoltra /api all'API.
#   docker build -f src/SunCash.Web/Dockerfile src/SunCash.Web
FROM node:24-alpine AS build
WORKDIR /web
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

FROM nginx:1.27-alpine
# L'immagine nginx sostituisce le variabili d'ambiente nei file di /etc/nginx/templates all'avvio.
# API_UPSTREAM: suncash-api:8080 nel compose, localhost:8080 su Azure (stessa app, stessa rete).
ENV API_UPSTREAM=suncash-api:8080
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /web/dist/suncash-web/browser /usr/share/nginx/html
EXPOSE 80
