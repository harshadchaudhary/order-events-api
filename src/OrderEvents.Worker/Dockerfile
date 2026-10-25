# Build context is the repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/OrderEvents.Contracts/OrderEvents.Contracts.csproj src/OrderEvents.Contracts/
COPY src/OrderEvents.Infrastructure/OrderEvents.Infrastructure.csproj src/OrderEvents.Infrastructure/
COPY src/OrderEvents.Worker/OrderEvents.Worker.csproj src/OrderEvents.Worker/
RUN dotnet restore src/OrderEvents.Worker/OrderEvents.Worker.csproj

COPY src/ src/
RUN dotnet publish src/OrderEvents.Worker/OrderEvents.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OrderEvents.Worker.dll"]
