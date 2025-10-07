# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy all project files and restore dependencies first to leverage Docker layer caching
COPY backend.fsproj .
COPY DbMigrations/Backend.DbMigrations.csproj ./DbMigrations/
RUN dotnet restore backend.fsproj

# Copy the rest of the source code
COPY . .

# Publish the main application
RUN dotnet publish backend.fsproj -c Release -o /app/publish

# Publish the migrations project to a temp folder, then copy the DLL to the publish folder
RUN dotnet publish DbMigrations/Backend.DbMigrations.csproj -c Release -o /tmp/mig
RUN cp /tmp/mig/Backend.DbMigrations.dll /app/publish/

# Stage 2: Create the final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# Expose the ports the app will listen on. 8080 for HTTP, 8081 for HTTPS.
EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "backend.dll"]