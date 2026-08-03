using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PaddleOCRSharp;

namespace PixOcrSearch
{
    public static class OcrHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("psapi.dll", EntryPoint = "EmptyWorkingSet", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        private static PaddleOCREngine? _engine;
        private static Task? _initTask;
        private static System.Threading.Timer? _disposeTimer;
        private static readonly object _lock = new object();

        // Start initialization asynchronously in the background
        public static void StartInitialize()
        {
            lock (_lock)
            {
                if (_engine == null && (_initTask == null || _initTask.IsFaulted || _initTask.IsCompleted))
                {
                    _initTask = Task.Run(() => InitializeInternal());
                }
            }
        }

        // Wait for the background initialization task to complete
        public static async Task EnsureInitializedAsync()
        {
            Task? task;
            lock (_lock)
            {
                task = _initTask;
            }

            if (task != null)
            {
                await task;
            }
            else
            {
                StartInitialize();
                lock (_lock)
                {
                    task = _initTask;
                }
                if (task != null)
                {
                    await task;
                }
            }
        }

        private static void InitializeInternal()
        {
            try
            {
                lock (_lock)
                {
                    if (_engine != null) return;
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string libsDir = Path.Combine(baseDir, "libs");
                SetDllDirectory(libsDir);

                // Initialize OCR parameters optimized for RAM footprint and speed balance
                OCRParameter ocrParameter = new OCRParameter
                {
                    cpu_math_library_num_threads = Math.Min(4, Environment.ProcessorCount), // Limit CPU threads but leverage multi-core
                    enable_mkldnn = false,            // Disable MKLDNN memory pool cache
                    cls = false,                      // Disable orientation classifier
                    det = true,                       // Enable detection
                    rec = true,                       // Enable recognition
                    max_side_len = 960                // Limit maximum image side length
                };

                // Create OCR model config pointing to lightweight "tiny" model paths
                OCRModelConfig config = new OCRModelConfig
                {
                    det_infer = Path.Combine(libsDir, "inference", "PP-OCRv6_tiny_det_infer"),
                    cls_infer = Path.Combine(libsDir, "inference", "PP-OCRv5_mobile_cls_infer"),
                    rec_infer = Path.Combine(libsDir, "inference", "PP-OCRv6_tiny_rec_infer"),
                    keys = Path.Combine(libsDir, "inference", "ppocr_keys.txt")
                };

                var engine = new PaddleOCREngine(config, ocrParameter);
                lock (_lock)
                {
                    _engine = engine;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("PaddleOCR 引擎初始化失败:\n" + ex.Message + "\n\n请确保 \"libs\" 目录及其中的依赖 DLL 和 inference 文件夹完整。", "OCR 错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public static async Task<string> RecognizeTextAsync(System.Drawing.Bitmap bitmap)
        {
            // Reset the inactivity timer when a screenshot is captured
            ResetDisposeTimer();

            // Ensure the engine has loaded in the background
            await EnsureInitializedAsync();

            string result = await Task.Run(() =>
            {
                try
                {
                    PaddleOCREngine? engine;
                    lock (_lock)
                    {
                        engine = _engine;
                    }

                    if (engine != null)
                    {
                        // Run OCR directly on the Bitmap to avoid MemoryStream/PNG allocations
                        // Synchronize access to the native PaddleOCR engine to ensure thread-safety
                        OCRResult ocrResult;
                        lock (engine)
                        {
                            ocrResult = engine.DetectText(bitmap);
                        }
                        
                        if (ocrResult != null && !string.IsNullOrEmpty(ocrResult.Text))
                        {
                            return ocrResult.Text.Trim();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("OCR 识别发生错误:\n" + ex.Message, "OCR 错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    // Reset timer again after OCR completes to start the 8s countdown
                    ResetDisposeTimer();
                }
                return string.Empty;
            });

            // Immediately run memory optimization and working set trim after OCR completes
            OptimizeMemory();

            return result;
        }

        public static void OptimizeMemory()
        {
            try
            {
                // Force .NET garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Trim physical memory pages back to OS standby list
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                EmptyWorkingSet(process.Handle);

                // Constrain physical memory working set to a fixed range (5MB to 40MB)
                process.MinWorkingSet = new IntPtr(5 * 1024 * 1024);
                process.MaxWorkingSet = new IntPtr(40 * 1024 * 1024);
            }
            catch { }
        }

        public static void Dispose()
        {
            lock (_lock)
            {
                if (_disposeTimer != null)
                {
                    _disposeTimer.Dispose();
                    _disposeTimer = null;
                }
                if (_engine != null)
                {
                    _engine.Dispose();
                    _engine = null;
                }
                _initTask = null;
            }
        }

        private static void ResetDisposeTimer()
        {
            lock (_lock)
            {
                if (_disposeTimer == null)
                {
                    _disposeTimer = new System.Threading.Timer(OnDisposeTimerFired, null, 8000, System.Threading.Timeout.Infinite);
                }
                else
                {
                    _disposeTimer.Change(8000, System.Threading.Timeout.Infinite);
                }
            }
        }

        private static void OnDisposeTimerFired(object? state)
        {
            Dispose();
            OptimizeMemory();
        }
    }
}
