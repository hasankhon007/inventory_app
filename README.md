# Inventory Hub

## Overview
This is the initial scaffold for the inventory management web app. It includes Identity, PostgreSQL support, localization (English + Uzbek), a theme toggle, and placeholder pages using table layouts.

## Prerequisites
- .NET SDK 8.x
- PostgreSQL

## Setup
1. Update the PostgreSQL connection string in appsettings.json.
2. (Optional) Set Google OAuth credentials in appsettings.json under Authentication:Google.
3. Create and apply the database migration:
   - dotnet tool install --global dotnet-ef
   - dotnet ef migrations add InitialCreate
   - dotnet ef database update

## Run
- dotnet run

## Deploy to Azure
This repository includes a GitHub Actions workflow at `.github/workflows/azure-webapp-deploy.yml` that builds and deploys `Course.WebApp` to Azure Web App.

Required GitHub repository secrets:
- `AZURE_WEBAPP_NAME`: Azure Web App name.
- `AZURE_WEBAPP_PUBLISH_PROFILE`: Publish profile XML downloaded from the Azure Portal.

Deployment triggers:
- Push to `main`.
- Manual run from the Actions tab (`workflow_dispatch`).

## Notes
- All tables are rendered in table views (no row-level action buttons).
- Search, theme, and language selection are available in the top header.
- Domain entities for inventories, items, fields, tags, access, and discussions are in place and use relational storage only.
