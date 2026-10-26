FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Ardan.slnx ./
COPY src ./src
COPY tests ./tests
RUN dotnet test tests/Ardan.Core.Tests -c Release
RUN dotnet publish src/Ardan.Api -c Release -o /out/api \
 && dotnet publish src/Ardan.Web -c Release -o /out/web

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
COPY deploy/start.sh ./start.sh
RUN chmod +x ./start.sh \
 && mkdir -p ./api/App_Data \
 && chown -R $APP_UID ./api/App_Data
USER $APP_UID
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    PORT=8080
EXPOSE 8080
CMD ["./start.sh"]
