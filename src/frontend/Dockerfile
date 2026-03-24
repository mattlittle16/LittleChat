FROM node:20-alpine@sha256:b88333c42c23fbd91596ebd7fd10de239cedab9617de04142dde7315e3bc0afa AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
ARG GIT_COMMIT=dev
COPY . .
RUN echo "{\"version\":\"$GIT_COMMIT\"}" > public/version.json
RUN npm run build

FROM nginx:alpine@sha256:f46cb72c7df02710e693e863a983ac42f6a9579058a59a35f1ae36c9958e4ce0 AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
