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

# Pick the command (default to run-all)
MODE=${1:-run-all}
shift || true   # drop it if present

echo -e "${CYAN}[entrypoint] Mode: ${MODE}${RESET}"

# Determine SQL command
case "$MODE" in
  run-all)
    SQL="EXEC tSQLt.RunAll"
    ;;
  run-class)
    if [ -z "${1-}" ]; then
      echo -e "${RED}[entrypoint] ERROR: you must supply a test class name${RESET}" >&2
      echo -e "${YELLOW}Usage: $0 run-class <CLASS_NAME>${RESET}" >&2
      exit 1
    fi
    SQL="EXEC tSQLt.RunTestClass '$1'"
    ;;
  run)
    if [ -z "${1-}" ]; then
      echo -e "${RED}[entrypoint] ERROR: you must supply a test name${RESET}" >&2
      echo -e "${YELLOW}Usage: $0 run <TEST_NAME>${RESET}" >&2
      exit 1
    fi
    SQL="EXEC tSQLt.Run '$1'"
    ;;
  *)  
  
    cat <<EOF >&2
${YELLOW}Usage: $0 [run-all|run-class CLASS_NAME|run TEST_NAME]

  run-all           — execute EXEC tSQLt.RunAll
  run-class NAME    — execute EXEC tSQLt.RunTestClass 'NAME'
  run NAME          — execute EXEC tSQLt.Run 'NAME'${RESET}
EOF
    exit 1
    ;;
esac

# Configure and create DB
echo -e "${CYAN}[entrypoint] Applying configuration and creating database...${RESET}"
EXECUTE_SQL="/opt/mssql-tools18/bin/sqlcmd -C -I -b -S $DB_SERVER,$DB_PORT -U sa -P $MSSQL_SA_PASSWORD"
$EXECUTE_SQL -d master -Q "EXEC sp_configure 'clr enabled', 1; RECONFIGURE;"
$EXECUTE_SQL -d master -Q "EXEC sp_configure 'show advanced options', 1; RECONFIGURE;"
$EXECUTE_SQL -d master -Q "EXEC sp_configure 'clr strict security', 0; RECONFIGURE;"
$EXECUTE_SQL -d master -Q "IF DB_ID('Eventuous') IS NULL BEGIN CREATE DATABASE Eventuous; END;"

echo -e "${CYAN}[entrypoint] Running tSQLt PrepareServer.sql${RESET}"
$EXECUTE_SQL -i /opt/tsqlt/PrepareServer.sql

# Install tSQLt schema if missing
for DB in Eventuous; do
  echo -e "${YELLOW}[entrypoint] Checking for tSQLt schema in ${DB}${RESET}"
  EXISTS=$(
    $EXECUTE_SQL -d "${DB}" -h -1 -W \
      -Q "SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'tSQLt') SELECT 1 ELSE SELECT 0"
  )
  if [ "${EXISTS}" -eq 0 ]; then
    echo -e "${GREEN}[entrypoint] Installing tSQLt into ${DB}${RESET}"
    $EXECUTE_SQL -d "${DB}" -i /opt/tsqlt/tSQLt.class.sql
  else
    echo -e "${CYAN}[entrypoint] tSQLt already present in ${DB}, skipping.${RESET}"
  fi
done

# Change directory to sqlpackage location
echo -e "${CYAN}[entrypoint] Publishing databases via sqlpackage...${RESET}"
cd /opt/sqlpackage

# Deploy Eventuous database
# This deploys the main database schema
echo -e "${CYAN}[entrypoint] Publishing Eventuous.SqlServer.Database.dacpac to Eventuous database...${RESET}"
./sqlpackage /q:True /a:Publish \
    /tsn:eventuous-db-from-dacpac /tdn:Eventuous \
    /tu:sa /tp:$MSSQL_SA_PASSWORD \
    /sf:/tmp/db/Eventuous.SqlServer.Database.dacpac \
    /TargetEncryptConnection:False \
    /ttsc:true \
    /p:ExcludeObjectTypes="Assemblies;Files;Logins;Users;Credentials;ApplicationRoles;DatabaseRoles;RoleMembership;ServerRoleMembership;ServerRoles;Certificates;MasterKeys;SymmetricKeys;DatabaseOptions;Permissions;" \
    /p:DropObjectsNotInSource=True \
    /p:BlockOnPossibleDataLoss=False \
    /p:AdditionalDeploymentContributors=AgileSqlClub.DeploymentFilterContributor \
    /p:AdditionalDeploymentContributorArguments="SqlPackageFilter0=IgnoreSchema(tSQLt);"

echo -e "${GREEN}[entrypoint] Eventuous.SqlServer.Database.dacpac published.${RESET}"

# Deploy tSQLt tests to Eventuous
# Note:
#   We cannot use `DropObjectsNotInSource=True` when publishing the UnitTest dacpac,
#   because the tSQLt tests are created via SQLCMD in a post-deployment script, and
#   therefore aren't tracked by the dacpac. If we dropped objects not in source,
#   sqlpackage would remove all tSQLt tests.
#
echo -e "${CYAN}[entrypoint] Publishing Eventuous.Tests.SqlServer.Database.dacpac to Eventuous database...${RESET}"

./sqlpackage /q:True /a:Publish \
    /tsn:eventuous-db-from-dacpac /tdn:Eventuous \
    /tu:sa /tp:$MSSQL_SA_PASSWORD \
    /sf:/tmp/db/Eventuous.Tests.SqlServer.Database.dacpac \
    /TargetEncryptConnection:False \
    /ttsc:true \
    /p:ExcludeObjectTypes="Assemblies;Files;Logins;Users;Credentials;ApplicationRoles;DatabaseRoles;RoleMembership;ServerRoleMembership;ServerRoles;Certificates;MasterKeys;SymmetricKeys;DatabaseOptions;Permissions;" \
    /p:DropObjectsNotInSource=False \
    /p:BlockOnPossibleDataLoss=False \
    /p:AdditionalDeploymentContributors=AgileSqlClub.DeploymentFilterContributor \
    /p:AdditionalDeploymentContributorArguments="SqlPackageFilter0=IgnoreSchema(tSQLt);"

echo -e "${GREEN}[entrypoint] Eventuous.Tests.SqlServer.Database.dacpac published.${RESET}"

# Run tests
echo -e "${CYAN}[entrypoint] Running tests: ${SQL}${RESET}"
$EXECUTE_SQL -d Eventuous -Q "$SQL;"
