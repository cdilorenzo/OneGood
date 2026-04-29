# Multi-stage build for OneGood API
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy only project files first for cached NuGet restore
COPY src/OneGood.Core/OneGood.Core.csproj src/OneGood.Core/
COPY src/OneGood.Infrastructure/OneGood.Infrastructure.csproj src/OneGood.Infrastructure/
COPY src/OneGood.Workers/OneGood.Workers.csproj src/OneGood.Workers/
COPY src/OneGood.Api/OneGood.Api.csproj src/OneGood.Api/
RUN dotnet restore src/OneGood.Api/OneGood.Api.csproj
RUN dotnet restore src/OneGood.Workers/OneGood.Workers.csproj

# Copy source and publish both API and Worker
COPY src/ src/
RUN dotnet publish src/OneGood.Api/OneGood.Api.csproj -c Release -o /app/publish --no-restore
RUN dotnet publish src/OneGood.Workers/OneGood.Workers.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY start-all.sh /app/start-all.sh
RUN chmod +x /app/start-all.sh

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV Database__Provider=Postgres
EXPOSE 8080

ENTRYPOINT ["/app/start-all.sh"]
