FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/ProcessIA.Web/ProcessIA.Web.csproj", "src/ProcessIA.Web/"]
RUN dotnet restore "src/ProcessIA.Web/ProcessIA.Web.csproj"
COPY . .
WORKDIR "/src/src/ProcessIA.Web"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProcessIA.Web.dll"]
