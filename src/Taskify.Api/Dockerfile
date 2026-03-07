# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution and project files
COPY *.slnx .
COPY Directory.Build.props .
COPY src/Taskify.Api/*.csproj ./src/Taskify.Api/
COPY src/Taskify.Shared/*.csproj ./src/Taskify.Shared/
COPY src/Taskify.ServiceDefaults/*.csproj ./src/Taskify.ServiceDefaults/

# Restore dependencies
RUN dotnet restore src/Taskify.Api/Taskify.Api.csproj

# Copy source code
COPY src/Taskify.Api/. ./src/Taskify.Api/
COPY src/Taskify.Shared/. ./src/Taskify.Shared/
COPY src/Taskify.ServiceDefaults/. ./src/Taskify.ServiceDefaults/

# Build and publish
WORKDIR /source/src/Taskify.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy published app
COPY --from=build /app/publish .

# Set ASP.NET Core to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080

# Run as non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "Taskify.Api.dll"]
