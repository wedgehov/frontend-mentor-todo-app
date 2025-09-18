# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first to leverage Docker layer caching
COPY backend.fsproj .
RUN dotnet nuget locals all --clear && dotnet restore backend.fsproj

# Copy the rest of the source code
COPY . .

# Publish the application for release
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Create the final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Expose the ports the app will listen on. 8080 for HTTP, 8081 for HTTPS.
EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "backend.dll"]