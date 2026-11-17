# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore from the project files only, so this layer stays cached until a dependency changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Domain/CqrsApiExample.Domain.csproj src/Domain/
COPY src/Application/CqrsApiExample.Application.csproj src/Application/
COPY src/Infrastructure/CqrsApiExample.Infrastructure.csproj src/Infrastructure/
COPY src/Contracts/CqrsApiExample.Contracts.csproj src/Contracts/
COPY src/Api/CqrsApiExample.Api.csproj src/Api/
RUN dotnet restore src/Api/CqrsApiExample.Api.csproj

COPY src/ src/
RUN dotnet publish src/Api/CqrsApiExample.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Non-root user provided by the .NET images.
USER $APP_UID
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CqrsApiExample.Api.dll"]
