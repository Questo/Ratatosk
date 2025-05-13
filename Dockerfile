# Stage 1: build + test
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . ./
RUN dotnet restore
RUN dotnet build --no-restore -c Release
RUN dotnet test tests/UnitTests --no-build --collect:"XPlat Code Coverage" --results-directory /coverage --logger trx
RUN dotnet publish src/API -c Release -o /publish

# Stage 2: minimal runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /publish ./
CMD ["dotnet", "Ratatosk.API.dll"]
