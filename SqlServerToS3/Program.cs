using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using LitS3;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace SqlServerToS3
{
    class Program
    {
        private string ServerName = ConfigurationSettings.AppSettings["ServerName"];
        private string Username = ConfigurationSettings.AppSettings["Username"];
        private string Password = ConfigurationSettings.AppSettings["Password"];
        private string DatabaseName = ConfigurationSettings.AppSettings["DatabaseName"];

        private string AccessKeyID = ConfigurationSettings.AppSettings["AccessKeyID"];
        private string SecretAccessKey = ConfigurationSettings.AppSettings["SecretAccessKey"];
        private string Bucket = ConfigurationSettings.AppSettings["Bucket"];

        private string TempFilePath = ConfigurationSettings.AppSettings["TempFilePath"];

        private int DaysToKeepS3BackupFor = int.Parse(ConfigurationSettings.AppSettings["DaysToKeepS3BackupFor"]);
        
        static void Main(string[] args)
        {
            Program program = new Program();
        }

        public Program()
        {
            var usingTrustedConnection = string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password);

            var sourceConnection = usingTrustedConnection
                ? new ServerConnection(ServerName) { LoginSecure = true }
                : new ServerConnection(ServerName, Username, Password);

            var sqlServer = new Server(sourceConnection);
            
            if(sqlServer != null){
                
                var backup = new Backup();
                var dbc = sqlServer.Databases;
                
                if (dbc.Contains(DatabaseName))
                {
                    backup.Action = BackupActionType.Database;

                    backup.Database = DatabaseName;

                    var dateFilename = DateTime.UtcNow.ToString("dd-MMM-yyyy");
                    var tempFilename = String.Format("{0}-{1}.bak", DatabaseName, dateFilename);
                    var tempBackupPath = String.Format("{0}{1}", TempFilePath, tempFilename);
                    
                    //remove old backups from this local temp location
                    foreach (var file in Directory.GetFiles(TempFilePath))
                    {
                        if (file != tempBackupPath)
                        {
                            Console.WriteLine("Removing previous temp backup " + file); 
                            File.Delete(file);
                        }
                    }

                    try
                    {
                        var backupDevice = new BackupDeviceItem(tempBackupPath, DeviceType.File);
                        backup.Devices.Add(backupDevice);
                        backup.Checksum = true;
                        backup.ContinueAfterError = false;
                        backup.LogTruncation = BackupTruncateLogType.Truncate;

                        //if file exists then do an incremental, otherwise do a full
                        if (File.Exists(tempBackupPath))
                        {
                            backup.Incremental = true;
                        }
                        else
                        {
                            backup.Incremental = false;
                        }

                        // Perform backup.
                        backup.SqlBackup(sqlServer);

                        //now move the backup to S3 - overwriting anything that is there with the same name
                        var s3 = new S3Service
                                     {
                                         AccessKeyID = AccessKeyID,
                                         SecretAccessKey = SecretAccessKey
                                     };

                        var bucket = Bucket;
                        s3.AddObject(tempBackupPath, bucket, tempFilename);

                        var metadataOnly = true;
                        
                        foreach(var listEntry in s3.ListObjects(Bucket,""))
                        {
                            var request = new GetObjectRequest(s3, Bucket, listEntry.Name, metadataOnly);

                            using (var response = request.GetResponse())
                            {
                                if (response.LastModified < DateTime.UtcNow.AddDays(DaysToKeepS3BackupFor * -1))
                                {
                                    Console.WriteLine("Going to delete old archive " + listEntry.Name); 
                                    s3.DeleteObject(Bucket,listEntry.Name);
                                }
                            }
                        }
                        Console.Out.WriteLine("Backup to S3 is complete");
                        System.Threading.Thread.Sleep(10000);
                    }
                    catch(Exception ee)
                    {
                        Console.Out.WriteLine("Exception occurred - do not continue.  Wait until next run to try again "+ee.ToString());
                        System.Threading.Thread.Sleep(10000);
                    }
                }
            }
        }
    }
}
