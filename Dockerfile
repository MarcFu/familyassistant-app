FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY src/FamilyAssist/FamilyAssist.csproj ./
RUN dotnet restore

COPY src/FamilyAssist/ ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Create non-root user
RUN adduser -D -h /app appuser

# HA Add-on data directory (writable by appuser)
RUN mkdir -p /data && chown appuser:appuser /data

COPY --from=build /app/publish .

# Install wget for healthcheck (alpine minimal)
RUN apk add --no-cache wget

ENV ASPNETCORE_URLS=http://+:8099
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8099

# Run as non-root
USER appuser

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://localhost:8099/ || exit 1

LABEL org.opencontainers.image.source="https://github.com/your-repo/familyassist" \
      org.opencontainers.image.description="FamilyAssist - Home Assistant Add-on for household management" \
      org.opencontainers.image.licenses="MIT"

ENTRYPOINT ["dotnet", "FamilyAssist.dll"]
