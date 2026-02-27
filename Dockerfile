# Stage 1: Build the frontend assets
FROM node:22-alpine AS frontend-build
WORKDIR /app/Client

# Install .NET SDK 9, which is required by vite-plugin-fable to build its daemon
RUN apk add --no-cache dotnet9-sdk

# Copy package files and install dependencies
COPY Client/package.json Client/package-lock.json ./
RUN npm install

# Copy the rest of the application source code
COPY Client .
COPY Shared /app/Shared

# Build the application using Vite
RUN npm run build

# Stage 2: Build the backend
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build
WORKDIR /src

# Copy solution and project files to restore dependencies
COPY FrontendMentorTodoApp.sln .
COPY Backend/Server/backend.fsproj Backend/Server/
COPY Backend/Entity/Entity.csproj Backend/Entity/
COPY Client/src/src.fsproj Client/src/
COPY Shared/Shared.fsproj Shared/

# Restore dependencies
RUN dotnet restore

# Copy the rest of the source code
COPY . .

# Publish the backend application
RUN dotnet publish Backend/Server/backend.fsproj -c Release -o /app/publish

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy the published backend
COPY --from=backend-build /app/publish .

# Copy the built frontend to the wwwroot folder to be served by the backend
COPY --from=frontend-build /app/Client/dist ./wwwroot

# Expose the port the app runs on
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "backend.dll"]
