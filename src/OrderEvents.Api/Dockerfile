# Build context is the repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only the project files first so `restore` is cached while source changes.
COPY src/OrderEvents.Contracts/OrderEvents.Contracts.csproj src/OrderEvents.Contracts/
COPY src/OrderEvents.Infrastructure/OrderEvents.Infrastructure.csproj src/OrderEvents.Infrastructure/
COPY src/OrderEvents.Api/OrderEvents.Api.csproj src/OrderEvents.Api/
RUN dotnet restore src/OrderEvents.Api/OrderEvents.Api.csproj

COPY src/ src/
RUN dotnet publish src/OrderEvents.Api/OrderEvents.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderEvents.Api.dll"]
