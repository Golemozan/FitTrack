FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY FitTrack.API/*.csproj ./FitTrack.API/
RUN dotnet restore FitTrack.API/FitTrack.API.csproj
COPY . .
RUN dotnet publish FitTrack.API/FitTrack.API.csproj -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 5000
CMD ["dotnet", "FitTrack.API.dll"]
