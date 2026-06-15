# ===== Build stage =====
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution + csproj trước để tận dụng cache layer restore
COPY MPS.sln ./
COPY src/MPS.API/MPS.API.csproj src/MPS.API/
COPY src/MPS.Application/MPS.Application.csproj src/MPS.Application/
COPY src/MPS.Domain/MPS.Domain.csproj src/MPS.Domain/
COPY src/MPS.Infrastructure/MPS.Infrastructure.csproj src/MPS.Infrastructure/
COPY src/MPS.Shared/MPS.Shared.csproj src/MPS.Shared/
RUN dotnet restore src/MPS.API/MPS.API.csproj

# Copy toàn bộ source và publish
COPY src/ src/
RUN dotnet publish src/MPS.API/MPS.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# ===== Runtime stage =====
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Chạy bằng user non-root có sẵn trong image (app, uid 1654)
USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MPS.API.dll"]
