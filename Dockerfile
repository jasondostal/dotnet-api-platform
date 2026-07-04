# Multi-stage build for the Accounts/Customers API. Built in-cloud by `az acr build`
# (no local Docker needed) or locally. Runs as the non-root APP_UID on port 8080.
# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore-layer: copy the project graph the API host depends on (Api -> Contracts,
# Platform.AspNetCore -> Platform, Integration) so layer caching survives source edits.
COPY src/Directory.Build.props src/
COPY src/ApiPlatform.Contracts/ApiPlatform.Contracts.csproj src/ApiPlatform.Contracts/
COPY src/ApiPlatform.Platform/ApiPlatform.Platform.csproj src/ApiPlatform.Platform/
COPY src/ApiPlatform.Platform.AspNetCore/ApiPlatform.Platform.AspNetCore.csproj src/ApiPlatform.Platform.AspNetCore/
COPY src/ApiPlatform.Integration/ApiPlatform.Integration.csproj src/ApiPlatform.Integration/
COPY src/ApiPlatform.Api/ApiPlatform.Api.csproj src/ApiPlatform.Api/
RUN dotnet restore src/ApiPlatform.Api/ApiPlatform.Api.csproj
COPY src/ src/
RUN dotnet publish src/ApiPlatform.Api/ApiPlatform.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ApiPlatform.Api.dll"]
