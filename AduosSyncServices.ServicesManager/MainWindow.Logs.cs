using AduosSyncServices.ServicesManager.Enums;
using AduosSyncServices.ServicesManager.Helpers;
using AduosSyncServices.ServicesManager.Resources;
using AduosSyncServices.ServicesManager.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AduosSyncServices.ServicesManager
{
    public partial class MainWindow
    {
        private void InitLogWatcher()
        {
            if (_logWatcher != null)
            {
                _logWatcher.EnableRaisingEvents = false;
                _logWatcher.Dispose();
                _logWatcher = null;
            }

            if (string.IsNullOrEmpty(_selectedService?.LogFolderPath) || !Directory.Exists(_selectedService.LogFolderPath))
                return;

            _logWatcher = new FileSystemWatcher(_selectedService.LogFolderPath, "*.txt")
            {
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };
            _logWatcher.Created += (_, _) => Dispatcher.InvokeAsync(TriggerLogReloadDebounced);
            _logWatcher.Deleted += (_, _) => Dispatcher.InvokeAsync(TriggerLogReloadDebounced);
        }

        private void TriggerLogReloadDebounced()
        {
            _logReloadDebounce.Stop();
            _logReloadDebounce.Start();
        }

        private async Task ShowLogsViewAsync()
        {
            ShowLogsView();

            LvLogFiles.ItemsSource = logFiles;
            IcLogLines.ItemsSource = _filteredLogLines;

            HookLogLinesScrollViewer();

            _logRefreshService.Start(TimeSpan.FromSeconds(5), RefreshTimer_TickAsync);

            await LoadLogFilesAsync();
        }

        private async Task RefreshTimer_TickAsync()
        {
            await RefreshServiceStatusAsync(CancellationToken.None);

            if (LogsViewContainer.Visibility != Visibility.Visible ||
                SelectedLogFile is not LogFileItem item ||
                string.IsNullOrEmpty(_currentPath)) return;

            var listBox = IcLogLines;
            if (listBox.Items.Count == 0) return;

            var sv = FindVisualChilds.FindVisualChild<ScrollViewer>(listBox);
            bool isAtBottom = sv != null &&
                              Math.Abs(sv.VerticalOffset - sv.ScrollableHeight) < 2;

            try
            {
                var newLines = await Task.Run(() => LogFileReader.ReadNewLines(_currentPath!, ref _lastReadOffset));
                if (newLines.Count > 0)
                {
                    _currentLogLines.AddRange(newLines.Select(_logService.ParseLogLine));
                    ApplyFilter();

                    int newWarnings = newLines.Count(l => l.Contains("WRN]", StringComparison.Ordinal));
                    int newErrors = newLines.Count(l => l.Contains("ERR]", StringComparison.Ordinal));

                    item.WarningsCount += newWarnings;
                    item.ErrorsCount += newErrors;

                    if (isAtBottom && sv != null)
                    {
                        await Dispatcher.BeginInvoke(() =>
                        {
                            listBox.ScrollIntoView(listBox.Items[^1]);
                        }, DispatcherPriority.Background);
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(string.Format(UiMessages.LogReadFailed, item.Name, ex.Message));
            }
        }

        private async Task LoadLogFilesAsync()
        {
            logFiles.Clear();
            if (_selectedService == null || !Directory.Exists(_selectedService.LogFolderPath)) return;

            try
            {
                var files = await _logService.GetLogFilesAsync(_selectedService.LogFolderPath);

                logFiles.Clear();
                foreach (var f in files)
                    logFiles.Add(f);

                if (logFiles.Count == 0)
                {
                    ResetLogView();
                    return;
                }

                if (logFiles.Count > 0 && SelectedLogFile == null)
                {
                    SelectedLogFile = logFiles[0];
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(string.Format(UiMessages.LogFilesLoadFailed, ex.Message));
            }
        }

        private async Task LoadEntireFileWithFilterAsync(LogFileItem item)
        {
            _currentLogLines.Clear();
            _isAtBottom = true;
            _currentPath = item.Path;

            var info = new FileInfo(item.Path);
            _lastReadOffset = info.Length;
            _loadedStartOffset = 0;
            _reachedFileStart = true;

            string[] allLines = Array.Empty<string>();

            await Task.Run(() =>
            {
                using var fs = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);

                var lines = new List<string>();
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }

                allLines = lines.ToArray();
            });

            var filteredLines = allLines
                .Select(_logService.ParseLogLine)
                .Where(l => l.Level == LogLevel.Error || l.Level == LogLevel.Warning)
                .ToList();

            _currentLogLines.AddRange(filteredLines);

            ApplyFilter();

            await Dispatcher.BeginInvoke(() =>
            {
                if (IcLogLines.Items.Count > 0)
                {
                    IcLogLines.UpdateLayout();
                    IcLogLines.ScrollIntoView(IcLogLines.Items[^1]);
                }
            }, DispatcherPriority.Background);
        }

        private void ApplyFilter()
        {
            _filteredLogLines.Clear();

            bool filter = ShowOnlyWarningsAndErrors;

            foreach (var line in _currentLogLines)
            {
                if (!filter || line.Level == LogLevel.Warning || line.Level == LogLevel.Error)
                    _filteredLogLines.Add(line);
            }

            if (_filteredLogLines.Count > 0 && _isAtBottom)
                IcLogLines.ScrollIntoView(_filteredLogLines[^1]);
        }

        private async Task HandleShowOnlyWarningsChangedAsync()
        {
            if (ShowOnlyWarningsAndErrors)
            {
                if (SelectedLogFile is LogFileItem item)
                {
                    await LoadEntireFileWithFilterAsync(item);
                }
            }
            else
            {
                await LoadSelectedFileContentAsync();
            }
        }

        private void IcLogLines_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var sv = e.OriginalSource as ScrollViewer;
            if (sv != null)
            {
                _isAtBottom = sv.VerticalOffset >= sv.ScrollableHeight - 1;
            }

            if (e.VerticalOffset <= 2)
                _ = LoadMoreAsync();
        }

        private void HookLogLinesScrollViewer()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var sv = GetScrollViewer(IcLogLines);
                if (sv != null)
                {
                    sv.ScrollChanged -= IcLogLines_ScrollChanged;
                    sv.ScrollChanged += IcLogLines_ScrollChanged;
                }
            }), DispatcherPriority.Loaded);
        }

        private ScrollViewer? GetScrollViewer(DependencyObject dep)
        {
            if (dep is ScrollViewer viewer)
                return viewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
            {
                var child = VisualTreeHelper.GetChild(dep, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private async Task LoadSelectedFileContentAsync()
        {
            _currentLogLines.Clear();
            _isAtBottom = true;

            if (SelectedLogFile is not LogFileItem item || !File.Exists(item.Path))
                return;

            _currentPath = item.Path;

            if (ShowOnlyWarningsAndErrors)
            {
                await LoadEntireFileWithFilterAsync(item);
                return;
            }

            try
            {
                _lastReadOffset = new FileInfo(item.Path).Length;

                var (lines, startOffset, reachedStart) =
                    await Task.Run(() => LogFileReader.ReadLastLines(item.Path, InitialTailLines));

                _loadedStartOffset = startOffset;
                _reachedFileStart = reachedStart;

                _currentLogLines.AddRange(lines.Select(_logService.ParseLogLine));
                ApplyFilter();

                await Dispatcher.BeginInvoke(() =>
                {
                    if (IcLogLines.Items.Count > 0)
                    {
                        IcLogLines.UpdateLayout();
                        IcLogLines.ScrollIntoView(IcLogLines.Items[^1]);
                        var sv = GetScrollViewer(IcLogLines);
                        sv?.ScrollToEnd();
                    }
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(string.Format(UiMessages.LogReadFailed, item.Name, ex.Message));
            }
        }

        private async Task LoadMoreAsync()
        {
            if (_isLoadingMore || _reachedFileStart || string.IsNullOrEmpty(_currentPath)) return;
            _isLoadingMore = true;

            try
            {
                var anchor = IcLogLines.Items.Count > 0 ? IcLogLines.Items[0] : null;

                var (older, newStart, reachedStart) =
                    await Task.Run(() => LogFileReader.ReadPreviousLines(_currentPath!, _loadedStartOffset, PageLines));

                if (older.Count > 0)
                {
                    _currentLogLines.InsertRange(0, older.Select(_logService.ParseLogLine));
                    ApplyFilter();
                    _loadedStartOffset = newStart;
                    _reachedFileStart = reachedStart;

                    if (anchor != null)
                    {
                        IcLogLines.UpdateLayout();
                        IcLogLines.ScrollIntoView(anchor);
                    }
                }
            }
            finally
            {
                _isLoadingMore = false;
            }
        }

        private void ResetLogView()
        {
            _logRefreshService.Stop();
            _currentLogLines.Clear();
            _filteredLogLines.Clear();
            _currentPath = null;
            _loadedStartOffset = 0;
            _lastReadOffset = 0;
            _reachedFileStart = false;
            _isAtBottom = true;
            _lastSelectedLog = null;
            TxtSelectedFileName.Text = string.Empty;
            _suppressLogSelection = true;
            SelectedLogFile = null;
            _suppressLogSelection = false;
        }
    }
}
