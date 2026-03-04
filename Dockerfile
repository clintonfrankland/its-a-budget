# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy everything (repo now contains only the Blazor app)
COPY . .

RUN dotnet restore ./ClintonFrankland.Blazor.csproj
RUN dotnet publish ./ClintonFrankland.Blazor.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ClintonFrankland.Blazor.dll"]
