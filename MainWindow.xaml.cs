using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GoogleDriveSync.GoogleDriveHandle;
using Google.Apis.Drive.v3;
using System.Windows.Media.Animation;
namespace GoogleDriveSync
{
    public partial class MainWindow : Window
    {
        public required ObservableCollection<FileSyncItem> fileSyncItems { get; set; }
        public const string dataName = "data.json";
        private DriveService service = null;
        public MainWindow()
        {
            InitializeComponent();
            InitList();
            LogIn();
        }

        #region Init
        private void InitList()
        {
            if (File.Exists(dataName))
            {
                try
                {
                    string jsonString = File.ReadAllText(dataName);
                    fileSyncItems = JsonSerializer.Deserialize<ObservableCollection<FileSyncItem>>(jsonString);
                }
                catch
                {
                    fileSyncItems = new() { };
                }
            }
            else
            {
                fileSyncItems = new() { };
            }

            FileSyncItemList.ItemsSource = fileSyncItems;
        }
        #endregion

        #region ButtonAnimation

        

        #endregion


        #region ButtonEvent
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var newItem = new FileSyncItem()
            {
                Index = (fileSyncItems.Count + 1).ToString(),
                FilePath = "",
                Url = "https://",
                Enable=true,
            };
            fileSyncItems.Add(newItem);
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedRow = FileSyncItemList.SelectedItem as FileSyncItem;
            if (selectedRow != null)
            {
                fileSyncItems.Remove(selectedRow);
                RefreshIndexes();
            }
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsActive(false);

            int totalFolders = fileSyncItems.Count(x=>x.Enable);
            Progress.Maximum = totalFolders;
            UpdateProgressSmoothly(0);
            int currentFolderIndex = 0;

            ProgressText.Content = $"准备开始：共{totalFolders}个文件夹需要同步";

            foreach (var item in fileSyncItems.Where(x=>x.Enable))
            {
                currentFolderIndex++;
                if (string.IsNullOrEmpty(item.FilePath) || string.IsNullOrEmpty(item.Url))
                {
                    continue;
                }
                try
                {
                    ProgressText.Content = $"正在同步第{currentFolderIndex}/{totalFolders}个文件夹：正在校验文件差异...";
                    var diffList = await DriveHelper.AnalyzeDifferences(service, item.FilePath, item.Url, IsIncludesSubfoldersCheckBox.IsChecked == true, true);
                    string parentid = DriveHelper.GetFolderIDFromURL(item.Url);

                    int totalDiffs = diffList.Count;
                    int currentDiffs = 0;
                    ProgressText.Content = $"正在同步第{currentFolderIndex}/{totalFolders}个文件夹：正在上传第{currentDiffs}/{totalDiffs}个差异文件：";
                    foreach (var diff in diffList)
                    {
                        if (diff.Status == EStatus.Same) continue;
                        currentDiffs++;
                        Action<long, long> updateProgressUI = (completed, total) =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                long compeletedMB = (long)(completed / 1024.0 / 1024.0);
                                long totalMB = (long)(total / 1024.0 / 1024.0);
                                ProgressText.Content = $"正在同步第{currentFolderIndex}/{totalFolders}个文件夹：正在上传第{currentDiffs}/{totalDiffs}个差异文件：{compeletedMB}MB/{totalMB}MB";
                                if (totalMB > 0 )
                                {
                                    UpdateProgressSmoothly((currentFolderIndex - 1) +
                                        (double)(((double)currentDiffs - 1) / (double)totalDiffs) + 
                                        ((double)((double)compeletedMB / (double)totalMB))*(double)(1/(double)totalDiffs))
                                        ;
                                }
                                else
                                {
                                    UpdateProgressSmoothly((currentFolderIndex - 1) + (double)(((double)currentDiffs-1)/ (double)totalDiffs));
                                }
                            });
                        };

                        switch (diff.Status)
                        {
                            case EStatus.UnDownload:
                                await DriveHelper.DeleteCloudFile(service, diff.CloudFileId);
                                break;
                            case EStatus.UnUpload:
                                await DriveHelper.UploadFile(service, Path.Combine(item.FilePath, diff.RelativePath, diff.FileName), parentid, diff.RelativePath, updateProgressUI);
                                break;
                            case EStatus.Diff:
                                await DriveHelper.DeleteCloudFile(service, diff.CloudFileId);
                                await DriveHelper.UploadFile(service, Path.Combine(item.FilePath, diff.RelativePath, diff.FileName), parentid, diff.RelativePath, updateProgressUI);
                                break;
                        }

                        UpdateProgressSmoothly((currentFolderIndex - 1) + (double)((double)currentDiffs / (double)totalDiffs));

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Upload failed: {ex.Message}");
                }
                UpdateProgressSmoothly(currentFolderIndex);
            }
            UpdateProgressSmoothly(totalFolders);
            ProgressText.Content = "所有文件夹同步完成!";
            SetButtonsActive(true);
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsActive(false);

            int totalFolders = fileSyncItems.Count(x => x.Enable);
            Progress.Maximum = totalFolders;
            UpdateProgressSmoothly(0);
            int currentFolderIndex = 0;

            ProgressText.Content = $"准备开始：共{totalFolders}个文件夹需要同步";

            foreach (var item in fileSyncItems.Where(x=>x.Enable))
            {
                currentFolderIndex++;

                if (string.IsNullOrEmpty(item.FilePath) || string.IsNullOrEmpty(item.Url))
                {
                    continue;
                }
                try
                {
                    ProgressText.Content = $"正在同步第{currentFolderIndex}/{totalFolders}个文件夹：正在校验文件差异...";
                    var diffList = await DriveHelper.AnalyzeDifferences(service, item.FilePath, item.Url, IsIncludesSubfoldersCheckBox.IsChecked == true, false);
                    string parentid = DriveHelper.GetFolderIDFromURL(item.Url);

                    int totalDiffs = diffList.Count;
                    int currentDiffs = 0;
                    ProgressText.Content = $"正在同步第{currentFolderIndex}/{totalFolders}个文件夹：正在下载第{currentDiffs}/{totalDiffs}个差异文件：";
                    foreach (var diff in diffList)
                    {
                        if (diff.Status == EStatus.Same) continue;
                        currentDiffs++;
                        Action<long, long> updateProgressUI = (completed, total) =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                long compeletedMB = (long)(completed / 1024.0 / 1024.0);
                                long totalMB = (long)(total / 1024.0 / 1024.0);

                                ProgressText.Content = $"正在同步第{currentFolderIndex}/{totalFolders}个文件夹：正在下载第{currentDiffs}/{totalDiffs}个差异文件：{compeletedMB}MB/{totalMB}MB";

                                if (totalMB > 0)
                                {
                                    UpdateProgressSmoothly((currentFolderIndex - 1) +
                                        (double)(((double)currentDiffs - 1) / (double)totalDiffs) +
                                        ((double)((double)compeletedMB / (double)totalMB)) * (double)(1 / (double)totalDiffs))
                                        ;
                                }
                                else
                                {
                                    UpdateProgressSmoothly((currentFolderIndex - 1) + (double)(((double)currentDiffs - 1) / (double)totalDiffs));
                                }

                            });
                        };

                        switch (diff.Status)
                        {
                            case EStatus.UnDownload:
                                await DriveHelper.DownloadFile(service, diff.CloudFileId, Path.Combine(item.FilePath, diff.RelativePath, diff.FileName), diff.Size, updateProgressUI);
                                break;
                            case EStatus.UnUpload:
                                if (File.Exists(Path.Combine(item.FilePath, diff.RelativePath, diff.FileName)))
                                {
                                    File.Delete(Path.Combine(item.FilePath, diff.RelativePath, diff.FileName));
                                }
                                break;
                            case EStatus.Diff:
                                if (File.Exists(Path.Combine(item.FilePath, diff.RelativePath, diff.FileName)))
                                {
                                    File.Delete(Path.Combine(item.FilePath, diff.RelativePath, diff.FileName));
                                }
                                await DriveHelper.DownloadFile(service, diff.CloudFileId, Path.Combine(item.FilePath, diff.RelativePath, diff.FileName), diff.Size, updateProgressUI);
                                break;
                        }

                        UpdateProgressSmoothly((currentFolderIndex - 1) + (double)((double)currentDiffs / (double)totalDiffs));

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Download failed: {ex.Message}");
                }
                UpdateProgressSmoothly(currentFolderIndex);
            }
            UpdateProgressSmoothly(totalFolders);
            ProgressText.Content = "所有文件夹同步完成!";
            SetButtonsActive(true);
        }



        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (fileSyncItems.Count == 0)
            {
                return;
            }
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(fileSyncItems, options);
                File.WriteAllText(dataName, jsonString);

                MessageBox.Show("Save Successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed:" + ex.Message);
            }
        }
        private void LogInButton_Click(object sender, RoutedEventArgs e)
        {
            LogIn();
        }
        #endregion

        #region Common Methods

        void RefreshIndexes()
        {
            for (int i = 0; i < fileSyncItems.Count; i++)
            {
                fileSyncItems[i].Index = (i + 1).ToString();
            }
            FileSyncItemList.Items.Refresh();
        }
        async void LogIn()
        {
            if (service == null)
            {
                try
                {
                    UploadButton.IsEnabled = false;
                    DownloadButton.IsEnabled = false;
                    service = await DriveHelper.GetDriveService();
                    LogInButton.Visibility = Visibility.Collapsed;
                    UploadButton.IsEnabled = true;
                    DownloadButton.IsEnabled = true;
                    MessageBox.Show("Log in successfully!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed:" + ex.Message);
                }
            }
        }

        void UpdateProgressSmoothly(double targetValue, int time = 1000)
        {
            if (targetValue < Progress.Value)
            {
                Progress.BeginAnimation(ProgressBar.ValueProperty, null);
                Progress.Value = targetValue;
                return;
            }
            DoubleAnimation animation = new DoubleAnimation(targetValue, TimeSpan.FromMilliseconds(time));
            animation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            Progress.BeginAnimation(ProgressBar.ValueProperty, animation);
        }

        void SetButtonsActive(bool b)
        {
            UploadButton.IsEnabled = b;
            DownloadButton.IsEnabled = b;
            AddButton.IsEnabled = b;
            RemoveButton.IsEnabled = b;
            SaveButton.IsEnabled = b;

        }

        #endregion

        #region WindowButtonEvent
        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion

    }
}