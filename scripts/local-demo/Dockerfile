FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet publish src/backend/IdentityPlatform.AuthServer/IdentityPlatform.AuthServer.csproj -c Release -o /publish/issuer -p:RestoreLockedMode=true
RUN dotnet publish src/backend/IdentityPlatform.Admin.Bff/IdentityPlatform.Admin.Bff.csproj -c Release -o /publish/bff -p:RestoreLockedMode=true
RUN dotnet publish src/backend/IdentityPlatform.Admin.Api/IdentityPlatform.Admin.Api.csproj -c Release -o /publish/api -p:RestoreLockedMode=true
RUN dotnet publish src/migrations/IdentityPlatform.Migrator/IdentityPlatform.Migrator.csproj -c Release -o /publish/migrator -p:RestoreLockedMode=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS issuer
WORKDIR /app
COPY --from=build /publish/issuer .
USER $APP_UID
ENTRYPOINT ["dotnet", "IdentityPlatform.AuthServer.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS bff
WORKDIR /app
COPY --from=build /publish/bff .
USER $APP_UID
ENTRYPOINT ["dotnet", "IdentityPlatform.Admin.Bff.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS api
WORKDIR /app
COPY --from=build /publish/api .
USER $APP_UID
ENTRYPOINT ["dotnet", "IdentityPlatform.Admin.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS migrator
WORKDIR /app
COPY --from=build /publish/migrator .
USER $APP_UID
ENTRYPOINT ["dotnet", "IdentityPlatform.Migrator.dll"]

FROM migrator AS initializer
COPY --chmod=0555 scripts/local-demo/register.sh /app/register.sh
ENTRYPOINT ["sleep", "infinity"]
