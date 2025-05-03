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

# Publish application
RUN dotnet publish src/API -c Release -o /publish

# Stage 2: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Copy published artifact from build stage
COPY --from=build /publish/. .

# Start the actual application
ENTRYPOINT ["dotnet", "Ratatosk.API.dll"]