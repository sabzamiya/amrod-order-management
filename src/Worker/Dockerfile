FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Api/Amrod.OrderManagement.Api.csproj", "src/Api/"]
COPY ["src/Worker/Amrod.OrderManagement.Worker.csproj", "src/Worker/"]

RUN dotnet restore "src/Worker/Amrod.OrderManagement.Worker.csproj"

COPY . .

WORKDIR "/src/src/Worker"

RUN dotnet publish "Amrod.OrderManagement.Worker.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Amrod.OrderManagement.Worker.dll"]