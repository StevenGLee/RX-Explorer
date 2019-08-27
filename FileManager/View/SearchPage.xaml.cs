﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class SearchPage : Page
    {
        public ObservableCollection<FileSystemStorageItem> SearchResult;
        StorageItemQueryResult ItemQuery;
        CancellationTokenSource Cancellation;

        public static SearchPage ThisPage { get; private set; }

        public QueryOptions SetSearchTarget
        {
            set
            {
                SearchResult.Clear();

                ItemQuery.ApplyNewQueryOptions(value);

                SearchPage_Loaded(null, null);
            }
        }

        public SearchPage()
        {
            InitializeComponent();
            ThisPage = this;
            SearchResult = new ObservableCollection<FileSystemStorageItem>();
            SearchResultList.ItemsSource = SearchResult;
        }

        private async void SearchPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SearchPage_Loaded;

            uint MaxSearchNum = uint.Parse(ApplicationData.Current.LocalSettings.Values["SetSearchResultMaxNum"] as string);

            HasItem.Visibility = Visibility.Collapsed;
            SearchResultList.Visibility = Visibility.Visible;

            LoadingControl.IsLoading = true;
            IReadOnlyList<IStorageItem> SearchItems = null;

            try
            {
                Cancellation = new CancellationTokenSource();
                Cancellation.Token.Register(() =>
                {
                    HasItem.Visibility = Visibility.Visible;
                    SearchResultList.Visibility = Visibility.Collapsed;
                    LoadingControl.IsLoading = false;
                });

                IAsyncOperation<IReadOnlyList<IStorageItem>> SearchAsync = ItemQuery.GetItemsAsync(0, MaxSearchNum);

                SearchItems = await SearchAsync.AsTask(Cancellation.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            finally
            {
                Cancellation.Dispose();
                Cancellation = null;
            }

            await Task.Delay(500);

            LoadingControl.IsLoading = false;

            if (SearchItems.Count == 0)
            {
                HasItem.Visibility = Visibility.Visible;
                SearchResultList.Visibility = Visibility.Collapsed;
            }
            else
            {
                List<IStorageItem> SortResult = new List<IStorageItem>(SearchItems.Count);
                SortResult.AddRange(SearchItems.Where((Item) => Item.IsOfType(StorageItemTypes.Folder)));
                SortResult.AddRange(SearchItems.Where((Item) => Item.IsOfType(StorageItemTypes.File)));

                foreach (var Item in SortResult)
                {
                    var Size = await Item.GetSizeDescriptionAsync();
                    var Thumbnail = await Item.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                    var ModifiedTime = await Item.GetModifiedTimeAsync();

                    SearchResult.Add(new FileSystemStorageItem(Item, Size, Thumbnail, ModifiedTime));
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ItemQuery = e.Parameter as StorageItemQueryResult;
            Loaded += SearchPage_Loaded;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SearchResult.Clear();
            Cancellation?.Cancel();
        }

        private async void Location_Click(object sender, RoutedEventArgs e)
        {
            var RemoveFile = SearchResultList.SelectedItem as FileSystemStorageItem;

            if (RemoveFile.ContentType == ContentType.Folder)
            {
                TreeViewNode TargetNode = await FindFolderLocationInTree(FileControl.ThisPage.FolderTree.RootNodes[0], new PathAnalysis(RemoveFile.Folder.Path));
                if (TargetNode == null)
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "无法定位文件夹，该文件夹可能已被删除或移动",
                        CloseButtonText = "确定",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    FileControl.ThisPage.CurrentNode = TargetNode;

                    while (true)
                    {
                        if (FileControl.ThisPage.FolderTree.ContainerFromNode(FileControl.ThisPage.CurrentNode) is TreeViewItem Item)
                        {
                            Item.IsSelected = true;
                            await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode);
                            break;
                        }
                        else
                        {
                            await Task.Delay(200);
                        }
                    }
                }
            }
            else
            {
                try
                {
                    _ = await StorageFile.GetFileFromPathAsync(RemoveFile.Path);

                    FileControl.ThisPage.CurrentNode = await FindFolderLocationInTree(FileControl.ThisPage.FolderTree.RootNodes[0], new PathAnalysis((await RemoveFile.File.GetParentAsync()).Path));
                    (FileControl.ThisPage.FolderTree.ContainerFromNode(FileControl.ThisPage.CurrentNode) as TreeViewItem).IsSelected = true;
                    await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode);
                }
                catch (FileNotFoundException)
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "无法定位文件，该文件可能已被删除或移动",
                        CloseButtonText = "确定",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                    _ = await dialog.ShowAsync();
                }
            }
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            IList<object> SelectedGroup = SearchResultList.SelectedItems;
            if (SelectedGroup.Count != 1)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "仅允许查看单个文件属性，请重试",
                    CloseButtonText = "确定",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                await dialog.ShowAsync();
            }
            else
            {
                FileSystemStorageItem Device = SelectedGroup.FirstOrDefault() as FileSystemStorageItem;
                if (Device.File != null)
                {
                    AttributeDialog Dialog = new AttributeDialog(Device.File);
                    await Dialog.ShowAsync();
                }
                else if (Device.Folder != null)
                {
                    AttributeDialog Dialog = new AttributeDialog(Device.Folder);
                    await Dialog.ShowAsync();
                }
            }
        }

        private async Task<TreeViewNode> FindFolderLocationInTree(TreeViewNode Node, PathAnalysis Analysis)
        {
            if (Node.HasUnrealizedChildren && !Node.IsExpanded)
            {
                FileControl.ThisPage.ExpenderLockerReleaseRequest = true;
                Node.IsExpanded = true;
            }
            else
            {
                FileControl.ThisPage.ExpandLocker.Set();
            }

            await Task.Run(() =>
            {
                FileControl.ThisPage.ExpandLocker.WaitOne();
            });

            string NextPathLevel = Analysis.NextPathLevel();

            if (NextPathLevel == Analysis.FullPath)
            {
                return (Node.Content as StorageFolder).Path == NextPathLevel ? Node : Node.Children.Where((SubNode) => (SubNode.Content as StorageFolder).Path == NextPathLevel).FirstOrDefault();
            }
            else
            {
                return await FindFolderLocationInTree((Node.Content as StorageFolder).Path == NextPathLevel ? Node : Node.Children.Where((SubNode) => (SubNode.Content as StorageFolder).Path == NextPathLevel).FirstOrDefault(), Analysis);
            }
        }

        private void SearchResultList_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            FileSystemStorageItem Context = (e.OriginalSource as FrameworkElement)?.DataContext as FileSystemStorageItem;
            SearchResultList.SelectedIndex = SearchResult.IndexOf(Context);
            e.Handled = true;
        }

        private void SearchResultList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Location.IsEnabled = SearchResultList.SelectedIndex != -1;
        }
    }
}