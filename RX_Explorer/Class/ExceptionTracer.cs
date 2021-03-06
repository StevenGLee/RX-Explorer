﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对错误的捕获和记录，以及蓝屏的导向
    /// </summary>
    public static class ExceptionTracer
    {
        private static readonly AutoResetEvent Locker = new AutoResetEvent(true);

        /// <summary>
        /// 请求进入蓝屏状态
        /// </summary>
        /// <param name="Ex">错误内容</param>
        public static async void RequestBlueScreen(Exception Ex)
        {
            if (Ex == null)
            {
                throw new ArgumentNullException(nameof(Ex), "Exception could not be null");
            }

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                string[] MessageSplit;
                try
                {
                    if (string.IsNullOrEmpty(Ex.Message))
                    {
                        MessageSplit = Array.Empty<string>();
                    }
                    else
                    {
                        MessageSplit = Ex.Message.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < MessageSplit.Length; i++)
                        {
                            MessageSplit[i] = "        " + MessageSplit[i];
                        }
                    }
                }
                catch
                {
                    MessageSplit = Array.Empty<string>();
                }

                string[] StackTraceSplit;
                try
                {
                    if (string.IsNullOrEmpty(Ex.StackTrace))
                    {
                        StackTraceSplit = Array.Empty<string>();
                    }
                    else
                    {
                        StackTraceSplit = Ex.StackTrace.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < StackTraceSplit.Length; i++)
                        {
                            StackTraceSplit[i] = "        " + StackTraceSplit[i].TrimStart();
                        }
                    }
                }
                catch
                {
                    StackTraceSplit = Array.Empty<string>();
                }

                if (Window.Current.Content is Frame rootFrame)
                {
                    string Message =
                    @$"Version: {string.Join('.', Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision)}
                        {Environment.NewLine}The following is the error message:
                        {Environment.NewLine}------------------------------------
                        {Environment.NewLine}Exception: {Ex.GetType().Name}
                        {Environment.NewLine}Message:
                        {Environment.NewLine}{(MessageSplit.Length == 0 ? "Unknown" : string.Join(Environment.NewLine, MessageSplit))}
                        {Environment.NewLine}Source：{(string.IsNullOrEmpty(Ex.Source) ? "Unknown" : Ex.Source)}
                        {Environment.NewLine}StackTrace：{Environment.NewLine}{(StackTraceSplit.Length == 0 ? "Unknown" : string.Join(Environment.NewLine, StackTraceSplit))}
                        {Environment.NewLine}------------------------------------{Environment.NewLine}";

                    rootFrame.Navigate(typeof(BlueScreen), Message);
                }
                else
                {
                    Frame Frame = new Frame();

                    Window.Current.Content = Frame;

                    string Message =
                    @$"Version: {string.Join('.', Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision)}
                        {Environment.NewLine}The following is the error message:
                        {Environment.NewLine}------------------------------------
                        {Environment.NewLine}Exception: {Ex.GetType().Name}
                        {Environment.NewLine}Message:
                        {Environment.NewLine}{(MessageSplit.Length == 0 ? "Unknown" : string.Join(Environment.NewLine, MessageSplit))}
                        {Environment.NewLine}Source：{(string.IsNullOrEmpty(Ex.Source) ? "Unknown" : Ex.Source)}
                        {Environment.NewLine}StackTrace：{Environment.NewLine}{(StackTraceSplit.Length == 0 ? "Unknown" : string.Join(Environment.NewLine, StackTraceSplit))}
                        {Environment.NewLine}------------------------------------{Environment.NewLine}";

                    Frame.Navigate(typeof(BlueScreen), Message);
                }
            });
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        /// <param name="Ex">错误</param>
        /// <returns></returns>
        public static async Task LogAsync(Exception Ex)
        {
            if (Ex == null)
            {
                throw new ArgumentNullException(nameof(Ex), "Exception could not be null");
            }

            await LogAsync(Ex.Message + Environment.NewLine + Ex.StackTrace).ConfigureAwait(false);
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        /// <param name="Message">错误消息</param>
        /// <returns></returns>
        public static async Task LogAsync(string Message)
        {
            await Task.Run(() =>
            {
                Locker.WaitOne();
            }).ConfigureAwait(false);

            string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            
            if (!string.IsNullOrEmpty(DesktopPath))
            {
                try
                {
                    StorageFolder DesktopFolder = await StorageFolder.GetFolderFromPathAsync(DesktopPath);
                    StorageFile TempFile = await DesktopFolder.CreateFileAsync("RX_Error_Message.txt", CreationCollisionOption.OpenIfExists);
                    await FileIO.AppendTextAsync(TempFile, Message + Environment.NewLine);
                }
                catch
                {
                    StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("RX_Error_Message.txt", CreationCollisionOption.OpenIfExists);
                    await FileIO.AppendTextAsync(TempFile, Message + Environment.NewLine);
                }
            }
            else
            {
                StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("RX_Error_Message.txt", CreationCollisionOption.OpenIfExists);
                await FileIO.AppendTextAsync(TempFile, Message + Environment.NewLine);
            }

            Locker.Set();
        }
    }
}
