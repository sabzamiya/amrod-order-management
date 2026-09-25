FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Api/Amrod.OrderManagement.Api.csproj", "src/Api/"]
RUN dotnet restore "src/Api/Amrod.OrderManagement.Api.csproj"

COPY . .
WORKDIR "/src/src/Api"
RUN dotnet publish "Amrod.OrderManagement.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Amrod.OrderManagement.Api.dll"]