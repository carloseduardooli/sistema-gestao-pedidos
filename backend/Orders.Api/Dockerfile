FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar os arquivos de projeto
COPY ["Orders.Api/Orders.Api.csproj", "Orders.Api/"]
COPY ["Orders.Core/Orders.Core.csproj", "Orders.Core/"]
COPY ["Orders.Infrastructure/Orders.Infrastructure.csproj", "Orders.Infrastructure/"]

# Restaurar as dependências
RUN dotnet restore "Orders.Api/Orders.Api.csproj"

# Copiar todo o código-fonte restante
COPY . .
WORKDIR "/src/Orders.Api"

# Publicar
RUN dotnet publish "Orders.Api.csproj" -c Release -o /app/publish

# Imagem de runtime final
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "Orders.Api.dll"]
