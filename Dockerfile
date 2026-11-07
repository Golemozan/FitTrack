# Tek servis: API hem veriyi hem de arayüzü aynı adresten sunar.
# Aynı origin olduğu için tarayıcı tarafında CORS devreye hiç girmez.

# ---- 1. Arayüzü statik dosyalara derle ----
FROM node:22-alpine AS client
WORKDIR /client
COPY fittrack-client/package.json fittrack-client/package-lock.json ./
RUN npm ci
COPY fittrack-client/ ./
RUN npm run build

# ---- 2. API'yi derle ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY FitTrack.API/*.csproj ./FitTrack.API/
RUN dotnet restore FitTrack.API/FitTrack.API.csproj
COPY FitTrack.API/ ./FitTrack.API/
RUN dotnet publish FitTrack.API/FitTrack.API.csproj -c Release -o out

# ---- 3. Çalışma imajı ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Gün sınırları kullanıcının saatine göre hesaplanır. Konteyner UTC kalırsa
# gece 00:00-03:00 arasında girilen her kayıt bir önceki güne düşer.
ENV TZ=Europe/Istanbul
RUN apt-get update \
 && apt-get install -y --no-install-recommends tzdata \
 && ln -snf /usr/share/zoneinfo/$TZ /etc/localtime \
 && echo $TZ > /etc/timezone \
 && rm -rf /var/lib/apt/lists/*

# Uygulama root olarak çalışmaz. Düz `USER app` yazılamaz: Railway volume'u root sahipli bağlar, veritabanı
# açılmaz. Konteyner root başlar, /var/data'yı devreder, sonra yetkisini bırakır. İmaj bu araçları
# taşımayı bırakırsa derleme burada kırılsın, canlıda değil.
RUN command -v setpriv && id app

WORKDIR /app
COPY --from=build /app/out .
COPY --from=client /client/dist ./wwwroot
EXPOSE 5000
# Satır içi: depo Windows'ta düzenleniyor, ayrı .sh dosyasında CRLF shebang'i bozar.
CMD ["sh", "-c", "if [ -d /var/data ]; then chown -R app:app /var/data; fi; export HOME=/home/app; exec setpriv --reuid=app --regid=app --init-groups --no-new-privs dotnet FitTrack.API.dll"]
