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
using System.Diagnostics;

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

        public static async Task<List<SyncDiffItem>> AnalyzeDifferences(DriveService service, string localFolderPath, string cloudFolderUrl, bool IsIncludingSubfolders = false, bool isUpload = false)
        {
            var resultList = new List<SyncDiffItem>();

            var localmap = await GetLocalMapResult(localFolderPath, IsIncludingSubfolders);
            var cloudMap = await GetCloudMapresult(service, cloudFolderUrl, IsIncludingSubfolders);

            foreach (var localFile in localmap)
            {
                var item = new SyncDiffItem
                {
                    FileName = localFile.Key.fileName,
                    RelativePath = localFile.Key.filePath,
                    LocalMD5 = localFile.Value.MD5,
                    Size = localFile.Value.Size,
                };
                if (cloudMap.ContainsKey(localFile.Key))
                {
                    var cloudInfo = cloudMap[localFile.Key];

                    item.CloudFileId = cloudInfo.id;
                    item.CloudMD5 = cloudInfo.Md5;
                    item.Size = isUpload ? item.Size : cloudInfo.Size;

                    if (item.LocalMD5 == item.CloudMD5)
                    {
                        item.Status = EStatus.Same;
                    }
                    else
                    {
                        item.Status = EStatus.Diff;
                    }
                }
                else
                {
                    item.Status = EStatus.UnUpload;
                }
                resultList.Add(item);
            }
            foreach (var cloudFile in cloudMap)
            {
                if (!localmap.ContainsKey(cloudFile.Key))
                {
                    var item = new SyncDiffItem
                    {
                        FileName = cloudFile.Key.fileName,
                        RelativePath = cloudFile.Key.filePath,
                        CloudMD5 = cloudFile.Value.Md5,
                        CloudFileId = cloudFile.Value.id,
                        Status = EStatus.UnDownload,
                        Size = cloudFile.Value.Size,
                    };
                    resultList.Add(item);
                }
            }

            //Debug:
            foreach (var result in resultList)
            {
                Debug.WriteLine($"FileName:{result.FileName},Path:{result.RelativePath},Status:{result.Status}");
            }
            return resultList;
        }

        #region AnalyzeDifference Common Methods
        static async Task<Dictionary<(string filePath, string fileName), (string MD5, long Size)>> GetLocalMapResult(string localFolderPath, bool isIncludingSubfolders = false)
        {
            if (!Directory.Exists(localFolderPath))
            {
                MessageBox.Show("Cannot Find Local Folder!");
                throw new Exception("Cannot Find Local Folder!");
            }
            var localFilesTask = Task.Run<Dictionary<(string, string), (string, long)>>(isIncludingSubfolders ? () => GetAllLocalFilesMap(localFolderPath) : () => GetLocalFilesMap(localFolderPath));

            await Task.WhenAll(localFilesTask);
            var localMap = localFilesTask.Result;
            return localMap;
        }
        static async Task<Dictionary<(string filePath, string fileName), (string id, string Md5, long Size)>> GetCloudMapresult(DriveService service, string url, bool isIncludingSubfolders = false)
        {

            string cloudFolderId = GetFolderIDFromURL(url);
            if (string.IsNullOrEmpty(cloudFolderId))
            {
                MessageBox.Show("Cannot Find Cloud Folder ID");
                throw new Exception("Cannot Find Local Folder!");
            }
            var cloudFilesTask = isIncludingSubfolders ? GetAllCloudFilesMap(service, cloudFolderId) : GetCloudFilesMap(service, cloudFolderId);

            await Task.WhenAll(cloudFilesTask);
            var cloudMap = cloudFilesTask.Result;
            return cloudMap;
        }
        #endregion


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
        public static async Task UploadFile(DriveService service, string localFilePath, string parentFolderId, string relativePath, Action<long, long> progressCallback = null)
        {
            if (!File.Exists(localFilePath)) return;

            string targetFolderID;
            if (string.IsNullOrEmpty(relativePath))
            {
                targetFolderID = parentFolderId;
            }
            else
            {
                targetFolderID = await GetOrCreateFolderPath(service, parentFolderId, relativePath);
            }

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = Path.GetFileName(localFilePath),
                Parents = new List<string> { targetFolderID }
            };

            using (var stream = new FileStream(localFilePath, FileMode.Open))
            {
                var request = service.Files.Create(fileMetadata, stream, "application/octet-stream");
                request.Fields = "id";

                if (progressCallback != null)
                {
                    request.ProgressChanged += (uploadProgress) =>
                    {
                        progressCallback(uploadProgress.BytesSent, stream.Length);
                    };
                }
                await request.UploadAsync();
            }
        }

        public static async Task DownloadFile(DriveService service, string fileId, string filePath, long totalBytes, Action<long, long> progressCallback = null)
        {
            if (string.IsNullOrEmpty(fileId)) return;

            try
            {
                var request = service.Files.Get(fileId);

                request.MediaDownloader.ProgressChanged += (downloadProgress) =>
                {
                    if (progressCallback != null)
                    {
                        progressCallback(downloadProgress.BytesDownloaded, totalBytes);
                    }
                };

                string dirPath = Path.GetDirectoryName(filePath);

                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

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
        static Dictionary<(string filePath, string fileName), (string MD5, long Size)> GetLocalFilesMap(string folderPath)
        {
            var map = new Dictionary<(string, string), (string, long)>();
            var files = Directory.GetFiles(folderPath);

            foreach (var filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                string md5 = GetLocalMD5(filePath);
                var info = new FileInfo(filePath);
                long size = info.Length;

                var key = (string.Empty, fileName);
                var value = (md5, info.Length);

                if (!map.ContainsKey(key))
                {
                    map.Add(key, value);
                }
            }
            return map;
        }

        static Dictionary<(string filePath, string fileName), (string MD5, long Size)> GetAllLocalFilesMap(string folderPath)
        {
            var map = new Dictionary<(string, string), (string, long)>();

            var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

            foreach (var fullPath in files)
            {
                var fileInfo = new FileInfo(fullPath);

                string fileName = Path.GetFileName(fullPath);

                string fileDirectory = Path.GetDirectoryName(fullPath);

                string relativePath = (fileDirectory == null) ? string.Empty : Path.GetRelativePath(folderPath, fileDirectory);

                string md5 = GetLocalMD5(fullPath);
                long size = fileInfo.Length;

                var key = (relativePath == "." ? "" : relativePath.Replace("\\", "/"), fileName);
                var value = (md5, size);
                if (!map.ContainsKey(key))
                {
                    map.Add(key, value);
                }
            }

            return map;

        }


        static async Task<Dictionary<(string filePath, string fileName), (string Id, string MD5, long Size)>> GetCloudFilesMap(DriveService service, string folderId)
        {
            var map = new Dictionary<(string filePath, string fileName), (string Id, string MD5, long Size)>();

            var request = service.Files.List();
            request.PageSize = 1000;
            request.Q = $"'{folderId}' in parents and trashed = false";
            request.Fields = "files(id, name, md5Checksum, size)";

            var result = await request.ExecuteAsync();
            if (result.Files != null)
            {
                foreach (var file in result.Files)
                {
                    var key = (string.Empty, file.Name);
                    if (!string.IsNullOrEmpty(file.Name) && !map.ContainsKey(key))
                    {
                        long size = file.Size.HasValue ? file.Size.Value : 0;
                        map.Add(key, (file.Id, file.Md5Checksum, size));
                    }
                }
            }
            return map;
        }

        static async Task<Dictionary<(string filePath, string fileName), (string Id, string MD5, long Size)>> GetAllCloudFilesMap(DriveService service, string folderId, string relativePath = "")
        {
            var map = new Dictionary<(string filePath, string fileName), (string Id, string MD5, long Size)>();

            var request = service.Files.List();
            request.PageSize = 1000;
            request.Q = $"'{folderId}' in parents and trashed = false";
            request.Fields = "nextPageToken, files(id, name, mimeType, md5Checksum, size)";

            string pageToken = null;
            do
            {
                request.PageToken = pageToken;
                var result = await request.ExecuteAsync();

                if (result.Files != null)
                {
                    foreach (var file in result.Files)
                    {
                        if (string.IsNullOrEmpty(file.Name)) continue;

                        if (file.MimeType == "application/vnd.google-apps.folder")
                        {
                            string subPath = System.IO.Path.Combine(relativePath, file.Name);

                            var subMap = await GetAllCloudFilesMap(service, file.Id, subPath);

                            foreach (var kvp in subMap)
                            {
                                if (!map.ContainsKey(kvp.Key))
                                {
                                    map.Add(kvp.Key, kvp.Value);
                                }
                            }
                        }
                        else
                        {
                            var key = (relativePath.Replace("\\", "/"), file.Name);

                            if (!map.ContainsKey(key))
                            {
                                string md5 = file.Md5Checksum ?? string.Empty;
                                long size = file.Size.HasValue ? file.Size.Value : 0;

                                map.Add(key, (file.Id, md5, size));
                            }
                        }
                    }
                }
            } while (pageToken != null);

            return map;
        }

        private static async Task<string> GetOrCreateFolderPath(DriveService service, string rootFolderId, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || relativePath == ".") return rootFolderId;

            string currentParentId = rootFolderId;
            var folders = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var folderName in folders)
            {
                var listRequest = service.Files.List();
                listRequest.Q = $"mimeType='application/vnd.google-apps.folder' and name='{folderName}' and '{currentParentId}' in parents and trashed=false";
                listRequest.Fields = "files(id)";
                var files = await listRequest.ExecuteAsync();

                if (files.Files != null && files.Files.Count > 0)
                {
                    currentParentId = files.Files[0].Id;
                }
                else
                {
                    var folderMetadata = new Google.Apis.Drive.v3.Data.File()
                    {
                        Name = folderName,
                        MimeType = "application/vnd.google-apps.folder",
                        Parents = new List<string> { currentParentId }
                    };
                    var request = service.Files.Create(folderMetadata);
                    request.Fields = "id";
                    var file = await request.ExecuteAsync();

                    currentParentId = file.Id;
                }
            }

            return currentParentId;
        }

        #endregion
    }

    public class SyncDiffItem
    {
        public string FileName { get; set; }
        public string RelativePath { get; set; }
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
