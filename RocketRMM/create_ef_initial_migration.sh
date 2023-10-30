#!/bin/sh

# Create the initial DB migrations

dotnet ef migrations add InitialCreate_LogsDbContext --context LogsDbContext -o "./Data/Migrations/LogsDbContext_Migrations"
dotnet ef migrations add InitialCreate_UserProfilesDbContext --context UserProfilesDbContext  -o "./Data/Migrations/UserProfilesDbContext_Migrations"

# Create the databases from latest migrations (read databases from update_databases.txt)

input="update_databases.txt"
while read -r line
do
  eval $line
done < "$input"

echo ""
echo "Script Done"
