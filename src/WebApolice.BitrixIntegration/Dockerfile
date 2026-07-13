FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/WebApolice.BitrixIntegration/WebApolice.BitrixIntegration.csproj", "src/WebApolice.BitrixIntegration/"]
RUN dotnet restore "src/WebApolice.BitrixIntegration/WebApolice.BitrixIntegration.csproj"
COPY . .
WORKDIR "/src/src/WebApolice.BitrixIntegration"
RUN dotnet build "WebApolice.BitrixIntegration.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebApolice.BitrixIntegration.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebApolice.BitrixIntegration.dll"]
