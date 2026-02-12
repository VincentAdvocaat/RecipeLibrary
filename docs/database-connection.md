# Database Connection & Tooling Guide

## Database Type

Het project gebruikt **SQL Server (MSSQL) 2022** die draait in een Docker container.

## Database Verbindingsgegevens

Wanneer de Docker container draait, gebruik je deze gegevens om te verbinden:

- **Server**: `localhost,1433` (of `127.0.0.1,1433`)
- **Database**: `RecipeLibrary`
- **Authenticatie**: SQL Server Authentication
- **Gebruikersnaam**: `sa`
- **Wachtwoord**: De waarde van `MSSQL_SA_PASSWORD` uit je `.env` bestand (standaard: `ChangeMe!123`)

**Connection String**:

```
Server=localhost,1433;Database=RecipeLibrary;User Id=sa;Password={jouw_wachtwoord};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

## Database Container Starten

Voordat je kunt verbinden, moet de database container draaien:

```powershell
docker compose up -d sql --wait
```

Controleer of de container draait:

```powershell
docker compose ps
```

De `sql` service moet **healthy** zijn.

## Tooling Installatie & Verbinding

### Optie 1: SQL Server Management Studio (SSMS) - Aanbevolen voor Windows

**SSMS** is de officiële Microsoft tool voor SQL Server management.

#### Installatie

1. Download SSMS van: <https://aka.ms/ssmsfullsetup>
2. Voer de installer uit en volg de wizard
3. SSMS installeert automatisch de SQL Server client tools

#### Verbinden met de database

1. Open **SQL Server Management Studio**
2. Klik op **Connect** → **Database Engine**
3. Vul in:
   - **Server name**: `localhost,1433` of `(local),1433`
   - **Authentication**: SQL Server Authentication
   - **Login**: `sa`
   - **Password**: De waarde uit je `.env` bestand (`MSSQL_SA_PASSWORD`)
4. Klik op **Connect**
5. Navigeer naar **Databases** → **RecipeLibrary** in Object Explorer

### Optie 2: Azure Data Studio - Cross-platform & Modern

**Azure Data Studio** is een moderne, cross-platform tool die ook werkt op macOS en Linux.

#### Installatie

1. Download Azure Data Studio van: <https://aka.ms/azuredatastudio>
2. Installeer de applicatie
3. (Optioneel) Installeer de **SQL Server Import** extensie voor extra functionaliteit

#### Verbinden met de database

1. Open **Azure Data Studio**
2. Klik op **New Connection** (of gebruik Ctrl+N)
3. Vul in:
   - **Connection type**: Microsoft SQL Server
   - **Server**: `localhost,1433`
   - **Authentication type**: SQL Login
   - **User name**: `sa`
   - **Password**: De waarde uit je `.env` bestand
   - **Database name**: `RecipeLibrary`
   - **Server group**: (optioneel)
   - **Name**: (optioneel, bijv. "Local RecipeLibrary")
4. Klik op **Connect**
5. De database verschijnt in de sidebar onder **Servers**

### Optie 3: Visual Studio Code met SQL Server Extensie

Als je VS Code gebruikt, kun je de SQL Server extensie installeren.

#### Installatie

1. Open VS Code
2. Ga naar **Extensions** (Ctrl+Shift+X)
3. Zoek naar **"SQL Server (mssql)"** door Microsoft
4. Klik op **Install**
5. Herstart VS Code indien nodig

#### Verbinden met de database

1. Druk op **Ctrl+Shift+P** om het command palette te openen
2. Type: `MS SQL: Connect`
3. Vul de connection string in:

   ```
   Server=localhost,1433;Database=RecipeLibrary;User Id=sa;Password={jouw_wachtwoord};Encrypt=True;TrustServerCertificate=True
   ```

   Of gebruik de wizard:
   - **Server**: `localhost,1433`
   - **Database**: `RecipeLibrary`
   - **Username**: `sa`
   - **Password**: De waarde uit je `.env` bestand
4. Klik op **Connect**
5. Je kunt nu SQL queries uitvoeren in `.sql` bestanden

## Troubleshooting

### Kan niet verbinden - "Network-related or instance-specific error"

- **Oplossing**: Controleer of de Docker container draait: `docker compose ps`
- Start de container: `docker compose up -d sql --wait`

### "Login failed for user 'sa'"

- **Oplossing**: Controleer of het wachtwoord overeenkomt met `MSSQL_SA_PASSWORD` in je `.env` bestand
- Het wachtwoord moet minimaal 8 karakters bevatten met hoofdletters, kleine letters, cijfers en een symbool

### "A network-related or instance-specific error occurred"

- **Oplossing**: Controleer of poort 1433 niet geblokkeerd wordt door een firewall
- Test de connectiviteit: `Test-NetConnection -ComputerName localhost -Port 1433` (PowerShell)

### Database "RecipeLibrary" bestaat niet

- **Oplossing**: De database wordt automatisch aangemaakt door Entity Framework migrations wanneer de applicatie start
- Start de web applicatie eenmaal om de database en schema aan te maken
- Of voer migrations handmatig uit via `dotnet ef database update`

## Handige SQL Queries

### Database status controleren

```sql
SELECT name, state_desc FROM sys.databases WHERE name = 'RecipeLibrary';
```

### Tabellen weergeven

```sql
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
```

### Verbindingen controleren

```sql
SELECT
    session_id,
    login_name,
    host_name,
    program_name,
    status
FROM sys.dm_exec_sessions
WHERE database_id = DB_ID('RecipeLibrary');
```

## Best Practices

1. **Gebruik altijd dezelfde wachtwoord** in `.env` als in de Docker container
2. **Start de container eerst** voordat je de applicatie of database tools start
3. **Gebruik SSMS of Azure Data Studio** voor complexe queries en database management
4. **Gebruik VS Code SQL extensie** voor snelle queries tijdens development
5. **Backup regelmatig** als je belangrijke data hebt (gebruik `docker compose exec sql /opt/mssql-tools18/bin/sqlcmd` voor backups)

## Gerelateerde Documentatie

- [Local Debug Guide](local-debug.md) — Voor het starten van de applicatie met lokale database
- [Docker Compose Config](../docker-compose.yml) — Database container configuratie
