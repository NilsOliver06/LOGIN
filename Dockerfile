# ============================================================
# 🍬👟 CANDY SHOES - DOCKERFILE CON KERBEROS
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# 🔥 INSTALAR LIBRERÍA KERBEROS (para evitar el error libgssapi_krb5.so.2)
RUN apt-get update && apt-get install -y \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 🔥 INSTALAR LIBRERÍA KERBEROS EN LA IMAGEN FINAL
RUN apt-get update && apt-get install -y \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "LOGIN.dll"]