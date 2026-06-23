# ============================================================
# 🍬👟 CANDY SHOES - DOCKERFILE CON DNS FORZADO
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# 🔥 CONFIGURAR DNS PARA RESOLVER SUPABASE
RUN echo "nameserver 8.8.8.8" > /etc/resolv.conf
RUN echo "nameserver 1.1.1.1" >> /etc/resolv.conf

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 🔥 CONFIGURAR DNS EN LA IMAGEN FINAL
RUN echo "nameserver 8.8.8.8" > /etc/resolv.conf
RUN echo "nameserver 1.1.1.1" >> /etc/resolv.conf

COPY --from=build /app/out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "LOGIN.dll"]