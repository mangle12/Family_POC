FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Family_POC/Family_POC.csproj", "Family_POC/"]
# unix 環境需額外安裝 libgdiplus 套件
RUN apt-get update && apt-get install -y libgdiplus
RUN dotnet restore "Family_POC/Family_POC.csproj"
COPY . .
WORKDIR "/src/Family_POC"
RUN dotnet build "Family_POC.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Family_POC.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Family_POC.dll"]