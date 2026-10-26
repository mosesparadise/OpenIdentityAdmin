FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /source
COPY . .
RUN dotnet publish src/backend/IdentityPlatform.AuthServer/IdentityPlatform.AuthServer.csproj -c Release -o /publish/issuer -p:RestoreLockedMode=true
RUN dotnet publish src/backend/IdentityPlatform.Admin.Bff/IdentityPlatform.Admin.Bff.csproj -c Release -o /publish/bff -p:RestoreLockedMode=true
RUN dotnet publish src/migrations/IdentityPlatform.Migrator/IdentityPlatform.Migrator.csproj -c Release -o /publish/migrator -p:RestoreLockedMode=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS issuer
WORKDIR /app
COPY --from=build /publish/issuer .
USER $APP_UID
ENTRYPOINT ["dotnet", "IdentityPlatform.AuthServer.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS bff
WORKDIR /app
COPY --from=build /publish/bff .
USER $APP_UID
ENTRYPOINT ["dotnet", "IdentityPlatform.Admin.Bff.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS migrator
WORKDIR /app
COPY --from=build /publish/migrator .
USER $APP_UID
ENTRYPOINT ["dotnet", "IdentityPlatform.Migrator.dll"]

FROM migrator AS initializer
COPY --chmod=0555 scripts/local-demo/register.sh /app/register.sh
ENTRYPOINT ["sleep", "infinity"]
