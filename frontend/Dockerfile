# Stage 1: Build the frontend assets
FROM node:22-alpine AS build
WORKDIR /app

# Install .NET SDK 8, which is required by vite-plugin-fable to build its daemon
RUN apk add --no-cache dotnet8-sdk

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

# Remove default Nginx content
RUN rm -rf ./*

# Copy built assets from the build stage
COPY --from=build /app/dist .

# Copy our custom Nginx configuration to overwrite the default
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]