FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["K8sDeploymentDemo.csproj", "."]
RUN dotnet restore "K8sDeploymentDemo.csproj"
COPY . .
RUN dotnet build "K8sDeploymentDemo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "K8sDeploymentDemo.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "K8sDeploymentDemo.dll"]