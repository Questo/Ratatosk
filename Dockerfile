# Stage 1: build and publish
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . ./
RUN dotnet restore
RUN dotnet build src/API -c Release --no-restore
RUN dotnet publish src/API -c Release -o /publish

# Also build test project so it's ready inside image
RUN dotnet build tests/UnitTests -c Release --no-restore

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /publish ./
COPY --from=build /src/tests ./tests

CMD ["dotnet", "Ratatosk.API.dll"]

