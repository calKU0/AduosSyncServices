using AduosSyncServices.ServicesManager.Enums;
using AduosSyncServices.ServicesManager.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AduosSyncServices.ServicesManager.Services
{
    public class LogService
    {
        public async Task<List<LogFileItem>> GetLogFilesAsync(string logFolderPath)
        {
            if (string.IsNullOrWhiteSpace(logFolderPath) || !Directory.Exists(logFolderPath))
                return new List<LogFileItem>();

            return await Task.Run(() =>
            {
                return Directory.GetFiles(logFolderPath, "*.txt")
                    .Select(CreateLogFileItem)
                    .Where(item => item != null)
                    .OrderByDescending(item => item!.Date)
                    .ToList()!;
            });
        }

        public LogLine ParseLogLine(string line)
        {
            var level = LogLevel.Information;
            if (line.Contains("ERR]", StringComparison.Ordinal)) level = LogLevel.Error;
            else if (line.Contains("WRN]", StringComparison.Ordinal)) level = LogLevel.Warning;
            return new LogLine { Level = level, Message = line };
        }

        private static LogFileItem? CreateLogFileItem(string filePath)
        {
            int warnings = 0;
            int errors = 0;

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    string? line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Contains("WRN]", StringComparison.Ordinal)) warnings++;
                        if (line.Contains("ERR]", StringComparison.Ordinal)) errors++;
                    }
                }

                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string datePart = fileName.Replace("log-", "");

                string formattedDate = fileName;
                DateTime? parsedDate = null;
                if (DateTime.TryParseExact(datePart, "yyyyMMdd", null, DateTimeStyles.None, out DateTime dt))
                {
                    formattedDate = dt.ToString("dd.MM.yyyy");
                    parsedDate = dt;
                }

                return new LogFileItem
                {
                    Name = formattedDate,
                    Path = filePath,
                    WarningsCount = warnings,
                    ErrorsCount = errors,
                    Date = parsedDate ?? DateTime.MinValue
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
