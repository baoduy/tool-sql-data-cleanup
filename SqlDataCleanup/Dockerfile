FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
ARG BUILDPLATFORM
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SqlDataCleanup.csproj", "./"]
RUN dotnet restore "SqlDataCleanup.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "SqlDataCleanup.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM --platform=$BUILDPLATFORM build AS publish
ARG TARGETARCH
ARG BUILDPLATFORM
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "SqlDataCleanup.csproj" -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    #/p:PublishSelfContained=true \
    #/p:PublishTrimmed=true \
    #/p:PublishSingleFile=true \
    -a $TARGETARCH

FROM --platform=$BUILDPLATFORM base AS final
ARG TARGETARCH
ARG BUILDPLATFORM

WORKDIR /app
COPY --from=publish /app/publish .
RUN rm appsettings.json
#ENTRYPOINT ["./SqlDataCleanup"]
ENTRYPOINT ["dotnet","SqlDataCleanup.dll"]

