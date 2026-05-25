using Foundation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UIKit;

namespace Fortress.iPhone.Autofill
{
    public static class ErrorLogger
    {
        private static readonly string LogFilePath = Path.Combine(
            NSSearchPath.GetDirectories(NSSearchPathDirectory.DocumentDirectory, 
                NSSearchPathDomain.User)[0] ?? "", "autofill_errors.log");
        
        // Buffer for log entries - flush on demand or at errors
        private static readonly List<string> _logBuffer = new List<string>();
        private static readonly object _lock = new object();
        
        // Set to false in release builds for better performance
#if DEBUG
        private static readonly bool _enableInfoLogging = true;
#else
        private static readonly bool _enableInfoLogging = false;
#endif

        public static void LogError(string message, Exception exception = null)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] ERROR: {message}";
                
                if (exception != null)
                {
                    logEntry += $"\nException: {exception.Message}";
                    logEntry += $"\nStackTrace: {exception.StackTrace}";
                }
                
                logEntry += "\n" + new string('-', 50) + "\n";
                
                // Also log to debug console
                Debug.WriteLine($"[AutoFill] {logEntry}");
                Console.WriteLine($"[AutoFill] {logEntry}");
                
                // Flush buffer and write error to file
                lock (_lock)
                {
                    _logBuffer.Add(logEntry);
                    FlushBufferInternal();
                }
            }
            catch
            {
                // Fail silently - we can't risk crashing the extension
                Debug.WriteLine($"[AutoFill] Failed to log error: {message}");
            }
        }

        public static void LogInfo(string message)
        {
            if (!_enableInfoLogging)
            {
                // In release, only log to console, skip file I/O
                Debug.WriteLine($"[AutoFill] INFO: {message}");
                return;
            }
            
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] INFO: {message}\n";
                
                // Only write to console - buffer for file
                Debug.WriteLine($"[AutoFill] {logEntry}");
                
                lock (_lock)
                {
                    _logBuffer.Add(logEntry);
                    
                    // Flush if buffer gets large
                    if (_logBuffer.Count >= 50)
                    {
                        FlushBufferInternal();
                    }
                }
            }
            catch
            {
                // Fail silently
                Debug.WriteLine($"[AutoFill] Failed to log info: {message}");
            }
        }
        
        /// <summary>
        /// Flush buffered log entries to disk
        /// </summary>
        public static void FlushBuffer()
        {
            lock (_lock)
            {
                FlushBufferInternal();
            }
        }
        
        private static void FlushBufferInternal()
        {
            if (_logBuffer.Count == 0) return;
            
            try
            {
                File.AppendAllText(LogFilePath, string.Join("", _logBuffer));
                _logBuffer.Clear();
            }
            catch
            {
                // Fail silently
            }
        }

        public static void ShowErrorAlert(UIViewController controller, string title, string message, 
            Action? onOk = null, bool canCancel = true)
        {
            try
            {
                var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
                
                alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ => 
                {
                    onOk?.Invoke();
                }));
                
                if (canCancel)
                {
                    alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, (UIAlertAction? _) => { }));
                }
                
                controller.InvokeOnMainThread(() => 
                {
                    controller.PresentViewController(alert, true, null);
                });
            }
            catch (Exception ex)
            {
                LogError($"Failed to show error alert: {title} - {message}", ex);
            }
        }

        public static string GetLogContents()
        {
            try
            {
                if (File.Exists(LogFilePath))
                    return File.ReadAllText(LogFilePath);
                return "No log file found.";
            }
            catch (Exception ex)
            {
                return $"Error reading log file: {ex.Message}";
            }
        }

        public static void ClearLogs()
        {
            try
            {
                if (File.Exists(LogFilePath))
                    File.Delete(LogFilePath);
                LogInfo("Log file cleared");
            }
            catch (Exception ex)
            {
                LogError("Failed to clear log file", ex);
            }
        }
    }
}