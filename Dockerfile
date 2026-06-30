# ============================================================
# 🍬👟 CANDY SHOES - DOCKERFILE CON KERBEROS
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Cambiar a root para tener permisos de instalación
USER root

# 🔥 INSTALAR LIBRERÍA KERBEROS EN ETAPA DE COMPILACIÓN
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Cambiar a root en la imagen final para poder instalar paquetes
USER root

# 🔥 [SOLUCIÓN CRÍTICA] INSTALAR LIBRERÍA KERBEROS EN LA IMAGEN FINAL
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out .

# Volver al usuario seguro por defecto de .NET 10 (Requerido por Render)
USER app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "LOGIN.dll"]