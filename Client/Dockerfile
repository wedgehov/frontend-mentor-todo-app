# Stage 1: Build the frontend assets
FROM node:22-alpine AS build
WORKDIR /app/Client

# Install .NET SDK 9, which is required by vite-plugin-fable to build its daemon
RUN apk add --no-cache dotnet9-sdk

# Prevent .NET first-run output from polluting daemon protocol stdout
ENV DOTNET_NOLOGO=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Copy package files and install dependencies
COPY Client/package.json ./
RUN npm install

# Copy the rest of the application source code
COPY Client .

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
COPY --from=build /app/Client/dist .

# Copy our custom Nginx configuration template and the entrypoint script
COPY Client/nginx.conf /etc/nginx/conf.d/default.conf.template
COPY Client/custom-entrypoint.sh /custom-entrypoint.sh
RUN chmod +x /custom-entrypoint.sh

EXPOSE 80

ENTRYPOINT ["/custom-entrypoint.sh"]
