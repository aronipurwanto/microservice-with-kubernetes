FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY PaymentApi/PaymentApi.csproj PaymentApi/
COPY PaymentCore/PaymentCore.csproj PaymentCore/
COPY PaymentModels/PaymentModels.csproj PaymentModels/
COPY PaymentData/PaymentData.csproj PaymentData/

# Restore dependencies
RUN dotnet restore PaymentApi/PaymentApi.csproj

# Copy everything else and build
COPY . .
WORKDIR /src/PaymentApi
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Create non-root user
RUN adduser --disabled-password --home /app --gecos '' appuser && chown -R appuser /app
USER appuser

EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "PaymentApi.dll"]