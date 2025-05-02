# Stage 1: Build and test
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy everything and restore
COPY . ./
RUN dotnet restore

# Build the solution
RUN dotnet build src/API -c Release --no-restore

# Run tests (fail fast if they break)
RUN dotnet test tests/UnitTests --no-restore --no-build --verbosity normal

RUN dotnet publish src/API -c Release -o /app/publish

# Stage 2: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final


WORKDIR /app
COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Ratatosk.API.dll"]
