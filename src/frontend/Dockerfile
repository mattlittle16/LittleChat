FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
ARG GIT_COMMIT=dev
COPY . .
RUN echo "{\"version\":\"$GIT_COMMIT\"}" > public/version.json
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
