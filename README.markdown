# SQL Server backup to S3

This script can be used to backup an EC2 database to an AWS S3 bucket on a regular basis by setting it up as a Scheduled Task.

Each day you run it it will create a new temp bak file in the location that you specify and backup to S3.  If you run further backups that day it will increment that backup file and backup to S3.  At the start of a new day it will delete the previous day's backup from the temp directory and start again.

It uses the great LitS3 library to write to S3

Please let me know if you run into any issues with it

The SMO dlls cannot be distributed with the source code due to licensing restrictions. You need to get Microsoft SQL Server 2008 R2 Shared Management Objects from the [Microsoft Download Center](http://www.microsoft.com/download/en/details.aspx?id=16978). Place `Microsoft.SqlServer.ConnectionInfo.dll`, `Microsoft.SqlServer.Management.Sdk.Sfc.dll`, `Microsoft.SqlServer.Smo.dll` and `Microsoft.SqlServer.SqlEnum.dll` in the `lib\SqlServerManagementObjects` directory.