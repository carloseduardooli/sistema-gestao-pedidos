FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar os arquivos de projeto
COPY ["Orders.Worker/Orders.Worker.csproj", "Orders.Worker/"]
COPY ["Orders.Core/Orders.Core.csproj", "Orders.Core/"]
COPY ["Orders.Infrastructure/Orders.Infrastructure.csproj", "Orders.Infrastructure/"]

# Restaurar as dependências
RUN dotnet restore "Orders.Worker/Orders.Worker.csproj"

# Copiar todo o código-fonte restante
COPY . .
WORKDIR "/src/Orders.Worker"

# Publicar
RUN dotnet publish "Orders.Worker.csproj" -c Release -o /app/publish

# Imagem de runtime final
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Orders.Worker.dll"]
