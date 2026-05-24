FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/HassCompanion/HassCompanion.csproj ./
RUN dotnet restore

COPY src/HassCompanion/ ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# HA Add-on data directory
RUN mkdir -p /data

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8099
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8099

ENTRYPOINT ["dotnet", "HassCompanion.dll"]
