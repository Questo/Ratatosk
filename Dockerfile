# Stage 1: build + test
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . ./
RUN dotnet restore
RUN dotnet build --no-restore -c Release
RUN dotnet test tests/UnitTests --collect:"XPlat Code Coverage" --logger "trx" --results-directory /coverage
RUN dotnet publish src/API -c Release -o /publish

# Stage 2: minimal runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /publish ./
COPY --from=build /coverage /coverage
CMD ["dotnet", "Ratatosk.API.dll"]
