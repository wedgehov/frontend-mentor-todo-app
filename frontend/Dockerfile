# Stage 1: Build the frontend assets
FROM node:22-alpine AS build
WORKDIR /app

# Install .NET SDK 9, which is required by vite-plugin-fable to build its daemon
RUN apk add --no-cache dotnet9-sdk

# Copy package files and install dependencies
COPY package.json package-lock.json ./
RUN npm install

# Copy the rest of the application source code AFTER installing dependencies
COPY . .

# Build the application using Vite
RUN npm run build

# Stage 2: Serve the static files with Nginx
FROM nginx:alpine
WORKDIR /usr/share/nginx/html

# Install 'gettext' which provides the 'envsubst' utility for variable substitution
RUN apk add --no-cache gettext

# Remove default Nginx content
RUN rm -rf ./*

# Copy built assets from the build stage
COPY --from=build /app/dist .

# Copy our custom Nginx configuration template and the entrypoint script
COPY nginx.conf /etc/nginx/conf.d/default.conf.template
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

EXPOSE 80

ENTRYPOINT ["/docker-entrypoint.sh"]