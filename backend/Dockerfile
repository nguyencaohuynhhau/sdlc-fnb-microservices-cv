# Một Dockerfile cho mọi dịch vụ: docker build --build-arg PROJECT=Ordering.Api backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN test -n "$PROJECT" && dotnet publish "$(find src -name "$PROJECT.csproj")" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG PROJECT
ENV APP=$PROJECT
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
# exec: dotnet nhận SIGTERM trực tiếp → tắt êm (outbox publisher dừng giữa hai lượt quét).
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP.dll\""]
