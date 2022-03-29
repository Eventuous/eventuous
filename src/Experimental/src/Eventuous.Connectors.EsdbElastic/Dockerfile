FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ./src ./src
RUN dotnet restore "src/Experimental/src/Eventuous.Connectors.EsdbElastic/Eventuous.Connectors.EsdbElastic.csproj"
WORKDIR "src/Experimental/src/Eventuous.Connectors.EsdbElastic"
RUN dotnet build "Eventuous.Connectors.EsdbElastic.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Eventuous.Connectors.EsdbElastic.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Eventuous.Connectors.EsdbElastic.dll"]
