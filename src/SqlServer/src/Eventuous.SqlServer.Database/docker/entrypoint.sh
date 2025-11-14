#!/usr/bin/env bash
set -euo pipefail

# ANSI colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
RESET='\033[0m'

# Get SA password from environment
MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD:?"${RED}Missing MSSQL_SA_PASSWORD environment variable${RESET}"}

# Ensure SQL Server's persistent directories exist on a fresh/empty volume.
# Without these, redirection to /var/opt/mssql/log/... will fail and SQL won't start.
mkdir -p /var/opt/mssql/{data,log}

# Make sure the mssql user owns the directories so sqlservr can read/write.
# Keep this if the container starts as root; you can omit it if you run as the mssql user.
chown -R mssql:mssql /var/opt/mssql


# Start SQL Server and grab its PID
echo -e "${CYAN}[entrypoint] Launching SQL Server...${RESET}"

# SQL Server is extremely verbose during bootup, we we're just going to completely silence it here.
/opt/mssql/bin/sqlservr >/var/opt/mssql/log/sqlservr.console 2>&1 &
SQL_PID=$!

# However, we still want to be notified when problems occur.
tail -F /var/opt/mssql/log/errorlog --pid $SQL_PID -v 2> /dev/null | grep -E 'Error:|Severity:' >&2 &

# Poll until the server is ready
echo -e "${YELLOW}[entrypoint] Waiting for SQL Server to accept connections...${RESET}"
until /opt/mssql-tools18/bin/sqlcmd \
      -S localhost \
      -U sa \
      -P "$MSSQL_SA_PASSWORD" \
      -C \
      -d master \
      -Q "SELECT 1" &>/dev/null; do
  sleep 1
  echo -e "${YELLOW}[entrypoint] Retrying SQL Server connection...${RESET}"
done

echo -e "${GREEN}[entrypoint] SQL Server is ready!${RESET}"

# Configure and create DB
echo -e "${CYAN}[entrypoint] Applying configuration and creating database...${RESET}"
EXECUTE_SQL="/opt/mssql-tools18/bin/sqlcmd -C -I -b -S localhost -U sa -P $MSSQL_SA_PASSWORD"
$EXECUTE_SQL -d master -Q "
    EXEC sp_configure 'clr enabled', 1;
    EXEC sp_configure 'show advanced options', 1;
    RECONFIGURE;
    EXEC sp_configure 'clr strict security', 0;
    RECONFIGURE;

    IF DB_ID('Eventuous') IS NULL
    BEGIN
        CREATE DATABASE Eventuous;
    END;
"

# Publish the DACPAC
echo -e "${CYAN}[entrypoint] Database Eventuous created. Publishing DACPAC...${RESET}"
cd /opt/sqlpackage
./sqlpackage /q:True /a:Publish \
  /tsn:localhost /tdn:Eventuous \
  /tu:sa /tp:$MSSQL_SA_PASSWORD \
  /sf:/tmp/db/Eventuous.SqlServer.Database.dacpac \
  /TargetEncryptConnection:False /ttsc:true \
  /p:ExcludeObjectTypes="Assemblies;Files;Logins;Users;Credentials;ApplicationRoles;DatabaseRoles;RoleMembership;ServerRoleMembership;ServerRoles;Certificates;MasterKeys;SymmetricKeys;DatabaseOptions;Permissions;" \
  /p:DropObjectsNotInSource=True \
  /p:BlockOnPossibleDataLoss=False

# Mark health
echo -e "${GREEN}[entrypoint] Eventuous database DACPAC publication complete.${RESET}"
touch /tmp/db/.ready

# Keep container alive
echo -e "\n${CYAN}[entrypoint] Waiting indefinitely. You can connect to this database server in SSMS now, as needed.${RESET}"
echo -e "${CYAN}[entrypoint] SSMS connect to:${RESET}"
echo -e "  ${YELLOW}Server: localhost,11433${RESET}"
echo -e "  ${YELLOW}Username: sa${RESET}"
echo -e "  ${YELLOW}Password: $MSSQL_SA_PASSWORD${RESET}"
echo -e "\n${CYAN}Ctrl+C to stop the server and exit the container${RESET}"

exec bash # interactive shell
