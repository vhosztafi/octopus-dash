FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT=EnergyDash.Api
WORKDIR /src
COPY api/ ./
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl tzdata && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
ARG PROJECT=EnergyDash.Api
ENV ENERGY_PROJECT=${PROJECT} ASPNETCORE_URLS=http://+:8080 TZ=Europe/London
USER app
ENTRYPOINT ["sh", "-c", "exec dotnet ${ENERGY_PROJECT}.dll"]
