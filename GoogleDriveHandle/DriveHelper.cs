using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;

namespace GoogleDriveSync.GoogleDriveHandle
{
    public class DriveHelper
    {
        private static string[] Scopes = { DriveService.Scope.Drive };

        private static string ApplicationName = "GoogleDriveSync";

        public static string GetFolderIDFromURL(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "";
            string maker = "folders/";
            int index = url.IndexOf(maker);
            if (index == -1)
            {
                return url.Trim();
            }
            string idPart = url.Substring(index + maker.Length);
            int questionMarkIndex = idPart.IndexOf("?");
            if (questionMarkIndex != -1)
            {
                idPart = idPart.Substring(0, questionMarkIndex);
            }

            int slashIndex = idPart.IndexOf("/");
            if (slashIndex != -1)
            {
                idPart = idPart.Substring(0, slashIndex);
            }

            return idPart;
        }
        public static async Task<DriveService> GetDriveService()
        {
            UserCredential credential;
            using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = "token";
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true));
            }

            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });


            return service;
        }

        public static async Task<List<SyncDiffItem>> AnalyzeDifferences(DriveService service, string localFolderPath, string cloudFolderUrl)
        {
            var resultList = new List<SyncDiffItem>();

            if (!Directory.Exists(localFolderPath))
            {
                MessageBox.Show("Can not find local Folder!");
                throw new Exception("Can not find local Folder!");
            }

            string cloudFolderId = GetFolderIDFromURL(cloudFolderUrl);
            if (string.IsNullOrEmpty(cloudFolderId))
            {
                MessageBox.Show("Can not find Cloud Folder ID!");
                throw new Exception("Can not find Cloud Folder ID!");
            }

            var localFilesTask = Task.Run(() => GetLocalFilesMap(localFolderPath));
            var cloudFilesTask = GetCloudFilesMap(service, cloudFolderId);

            await Task.WhenAll(localFilesTask, cloudFilesTask);

            var localMap = localFilesTask.Result;
            var cloudMap = cloudFilesTask.Result;

            foreach (var localFile in localMap)
            {
                string fileName = localFile.Key;
                var localInfo = localFile.Value;

                var item = new SyncDiffItem
                {
                    FileName = fileName,
                    LocalFilePath = localInfo.Path,
                    LocalMD5 = localInfo.MD5,
                    Size=localInfo.Size,
                };
                if (cloudMap.ContainsKey(fileName))
                {
                    var cloudInfo = cloudMap[fileName];
                    item.CloudFileId = cloudInfo.Id;
                    item.CloudMD5 = cloudInfo.MD5;
                    item.Size=cloudInfo.Size;

                    if (item.LocalMD5 == item.CloudMD5)
                    {
                        item.Status = EStatus.Same;
                    }
                    else
                    {
                        item.Status = EStatus.Diff;
                    }

                    cloudMap.Remove(fileName);
                }
                else
                {
                    item.Status = EStatus.UnUpload; // Local Only
                }

                resultList.Add(item);
            }
            foreach (var cloudFile in cloudMap)
            {
                var item = new SyncDiffItem
                {
                    FileName = cloudFile.Key,
                    CloudFileId = cloudFile.Value.Id,
                    CloudMD5 = cloudFile.Value.MD5,
                    LocalFilePath = Path.Combine(localFolderPath, cloudFile.Key),
                    Status = EStatus.UnDownload, // Cloud Only
                    Size=cloudFile.Value.Size
                };
                resultList.Add(item);
            }


            return resultList;
        }

        public static async Task DeleteCloudFile(DriveService service, string folderId)
        {
            if (string.IsNullOrEmpty(folderId)) return;
            try
            {
                await service.Files.Delete(folderId).ExecuteAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete Failed:{ex.Message}");
            }
        }
        public static async Task UploadFile(DriveService service, string localFilePath, string parentFolderId,Action<long,long> progressCallback=null)
        {
            if (!File.Exists(localFilePath)) return;

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = Path.GetFileName(localFilePath),
                Parents = new List<string> { parentFolderId }
            };

            using (var stream = new FileStream(localFilePath, FileMode.Open))
            {
                var request = service.Files.Create(fileMetadata, stream, "application/octet-stream");
                request.Fields = "id";

                if(progressCallback != null)
                {
                    request.ProgressChanged += (uploadProgress) =>
                    {
                        progressCallback(uploadProgress.BytesSent,stream.Length);
                    };
                }
                await request.UploadAsync();
            }
        }

        public static async Task DownloadFile(DriveService service, string fileId, string filePath,long totalBytes, Action<long, long> progressCallback = null)
        {
            if (string.IsNullOrEmpty(fileId)) return;
            try
            {
                var request = service.Files.Get(fileId);

                request.MediaDownloader.ProgressChanged += (downloadProgress) =>
                {
                    if(progressCallback != null)
                    {
                        progressCallback(downloadProgress.BytesDownloaded, totalBytes);
                    }
                };


                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    await request.DownloadAsync(stream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download Failed:{ex.Message}");
            }
        }

        #region private methods

        static string GetLocalMD5(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
        static async Task<IList<Google.Apis.Drive.v3.Data.File>> GetCloudFiles(DriveService service, string folderId)
        {
            var listRequest = service.Files.List();

            listRequest.PageSize = 1000;

            listRequest.Q = $"'{folderId}' in parents and trashed = false";

            listRequest.Fields = "nextPageToken, files(id, name, md5Checksum)";

            var result = await listRequest.ExecuteAsync();
            return result.Files;
        }
        static Dictionary<string, (string Path, string MD5, long Size)> GetLocalFilesMap(string folderPath)
        {
            var map = new Dictionary<string, (string, string, long)>();
            var files = Directory.GetFiles(folderPath);

            foreach (var filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                string md5 = GetLocalMD5(filePath);
                var info=new FileInfo(filePath);
                if (!map.ContainsKey(fileName))
                {
                    map.Add(fileName, (filePath, md5,info.Length));
                }
            }
            return map;
        }
        static async Task<Dictionary<string, (string Id, string MD5 ,long Size)>> GetCloudFilesMap(DriveService service, string folderId)
        {
            var map = new Dictionary<string, (string, string, long)>();

            var request = service.Files.List();
            request.PageSize = 1000;
            request.Q = $"'{folderId}' in parents and trashed = false";
            request.Fields = "files(id, name, md5Checksum, size)";

            var result = await request.ExecuteAsync();
            if (result.Files != null)
            {
                foreach (var file in result.Files)
                {
                    if (!string.IsNullOrEmpty(file.Name) && !map.ContainsKey(file.Name))
                    {
                        long size = file.Size.HasValue ? file.Size.Value : 0;
                        map.Add(file.Name, (file.Id, file.Md5Checksum, size));
                    }
                }
            }
            return map;
        }
        #endregion
    }

    public class SyncDiffItem
    {
        public string FileName { get; set; }
        public string LocalFilePath { get; set; }
        public string CloudFileId { get; set; }
        public string LocalMD5 { get; set; }
        public string CloudMD5 { get; set; }

        public long Size { get; set; }
        public EStatus Status { get; set; }
    }

    public enum EStatus
    {
        Same,
        Diff,
        UnUpload,
        UnDownload
    }
}
