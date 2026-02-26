# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore first for better cache reuse
COPY Backend/Server/backend.fsproj Backend/Server/
COPY Backend/Entity/Entity.fsproj Backend/Entity/
COPY Backend/Entity/Migrations/Backend.Migrations.csproj Backend/Entity/Migrations/
RUN dotnet restore Backend/Server/backend.fsproj

# Copy source code
COPY Backend/Server Backend/Server
COPY Backend/Entity Backend/Entity

# Publish the main application
RUN dotnet publish Backend/Server/backend.fsproj -c Release -o /app/publish

# Publish migrations and copy the DLL to runtime output
RUN dotnet publish Backend/Entity/Migrations/Backend.Migrations.csproj -c Release -o /tmp/mig
RUN cp /tmp/mig/Backend.Migrations.dll /app/publish/

# Stage 2: Create the final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# Expose the ports the app will listen on. 8080 for HTTP, 8081 for HTTPS.
EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "backend.dll"]
