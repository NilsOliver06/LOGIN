# ============================================================
# 🍬👟 CANDY SHOES - DOCKERFILE PARA RENDER
# ============================================================
# Usa .NET 8 SDK para compilar
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar archivo de proyecto y restaurar dependencias
COPY *.csproj .
RUN dotnet restore

# Copiar todo el código y compilar
COPY . .
RUN dotnet publish -c Release -o out

# ============================================================
# Imagen final para ejecutar la aplicación
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

# Puerto que usará Render
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Comando para iniciar la aplicación
ENTRYPOINT ["dotnet", "LOGIN.dll"]