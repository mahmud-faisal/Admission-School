# syntax=docker/dockerfile:1

# ---- build: restore (cached on project files) and compile everything, tests included ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props SchoolAdmission.slnx ./
COPY .config/ .config/
COPY src/SchoolAdmission.Domain/SchoolAdmission.Domain.csproj src/SchoolAdmission.Domain/
COPY src/SchoolAdmission.Application/SchoolAdmission.Application.csproj src/SchoolAdmission.Application/
COPY src/SchoolAdmission.Infrastructure/SchoolAdmission.Infrastructure.csproj src/SchoolAdmission.Infrastructure/
COPY src/SchoolAdmission.Api/SchoolAdmission.Api.csproj src/SchoolAdmission.Api/
COPY tests/SchoolAdmission.UnitTests/SchoolAdmission.UnitTests.csproj tests/SchoolAdmission.UnitTests/
COPY tests/SchoolAdmission.IntegrationTests/SchoolAdmission.IntegrationTests.csproj tests/SchoolAdmission.IntegrationTests/
RUN dotnet restore SchoolAdmission.slnx
COPY . .
RUN dotnet build SchoolAdmission.slnx -c Release --no-restore

# ---- test: `docker compose --profile test run --rm backend-tests` (needs TEST_DATABASE_URL) ----
FROM build AS test
# The two-process test starts this dll twice as separate OS processes.
ENV API_DLL_PATH=/src/tests/SchoolAdmission.IntegrationTests/bin/Release/net10.0/SchoolAdmission.Api.dll
ENTRYPOINT ["dotnet", "test", "SchoolAdmission.slnx", "-c", "Release", "--no-build", "--logger", "console;verbosity=normal"]

# ---- publish ----
FROM build AS publish
RUN dotnet publish src/SchoolAdmission.Api/SchoolAdmission.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---- runtime: the API on port 8080 ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
