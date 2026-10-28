# Frontend: build Angular, poi nginx che serve i file e inoltra /api all'API.
#   docker build -f src/SunCash.Web/Dockerfile src/SunCash.Web
FROM node:24-alpine AS build
WORKDIR /web
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist/suncash-web/browser /usr/share/nginx/html
EXPOSE 80
