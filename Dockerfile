FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY src/FamilyAssistant/FamilyAssistant.csproj ./
RUN dotnet restore

COPY src/FamilyAssistant/ ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# ICU for globalization/localization support (de/en/fr)
RUN apk add --no-cache icu-libs wget
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .

# HA Add-on data directory
RUN mkdir -p /data

ENV ASPNETCORE_URLS=http://+:8099
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8099

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://localhost:8099/ || exit 1

LABEL org.opencontainers.image.source="https://github.com/MarcFu/familyassistant-app" \
      org.opencontainers.image.description="FamilyAssistant - Home Assistant App for household management" \
      org.opencontainers.image.licenses="MIT"

ENTRYPOINT ["dotnet", "FamilyAssistant.dll"]
