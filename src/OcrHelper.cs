using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using PaddleOCRSharp;

namespace PixOcrSearch
{
    public static class OcrHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("psapi.dll", EntryPoint = "EmptyWorkingSet", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("mklml.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "MKL_Free_Buffers", CharSet = CharSet.Ansi)]
        private static extern void MKL_Free_Buffers();

        [DllImport("mklml.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "mkl_free_buffers", CharSet = CharSet.Ansi)]
        private static extern void mkl_free_buffers();

        private static PaddleOCREngine? _engine;
        private static Task? _initTask;
        private static System.Threading.Timer? _disposeTimer;
        private static string _currentModel = "";
        private static readonly object _lock = new object();

        // Start initialization asynchronously in the background using the user-selected model
        public static void StartInitialize()
        {
            string modelToLoad = ConfigManager.Current.OcrModel;
            lock (_lock)
            {
                if (_engine == null || _currentModel != modelToLoad)
                {
                    // If model is changing, dispose old engine first to avoid memory leaks
                    if (_engine != null)
                    {
                        _engine.Dispose();
                        _engine = null;
                    }
                    _currentModel = modelToLoad;
                    _initTask = Task.Run(() => InitializeInternal(modelToLoad));
                }
            }
        }

        // Wait for the background initialization task to complete
        public static async Task EnsureInitializedAsync()
        {
            string modelToLoad = ConfigManager.Current.OcrModel;
            Task? task;
            lock (_lock)
            {
                if (_engine == null || _currentModel != modelToLoad)
                {
                    StartInitialize();
                }
                task = _initTask;
            }

            if (task != null)
            {
                await task;
            }
        }

        private static void InitializeInternal(string model)
        {
            try
            {
                // Suppress PaddlePaddle C++ glog output to optimize console writing overhead and speed up execution
                Environment.SetEnvironmentVariable("GLOG_minloglevel", "3");

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string libsDir = Path.Combine(baseDir, "libs");
                SetDllDirectory(libsDir);

                // Initialize OCR parameters optimized for RAM footprint and speed balance
                OCRParameter ocrParam = new OCRParameter
                {
                    cpu_math_library_num_threads = Math.Min(4, Environment.ProcessorCount), // Restrict threads to 4 to avoid native crashes under heavy load
                    enable_mkldnn = true,             // Enable MKLDNN for fast CPU inference (improves speed by 2x-4x)
                    cls = false,                      // Disable orientation classifier
                    det = true,                       // Enable detection
                    rec = true,                       // Enable recognition
                    max_side_len = 960                // Limit maximum image side length
                };

                string inferenceDir = Path.Combine(libsDir, "inference");

                string detModel = "PP-OCRv6_tiny_det_infer";
                string recModel = "PP-OCRv6_tiny_rec_infer";

                bool tinyExists = Directory.Exists(Path.Combine(inferenceDir, "PP-OCRv6_tiny_det_infer")) && Directory.Exists(Path.Combine(inferenceDir, "PP-OCRv6_tiny_rec_infer"));
                bool smallExists = Directory.Exists(Path.Combine(inferenceDir, "PP-OCRv6_small_det_infer")) && Directory.Exists(Path.Combine(inferenceDir, "PP-OCRv6_small_rec_infer"));

                if (model == "PP-OCRv6_small" && smallExists)
                {
                    detModel = "PP-OCRv6_small_det_infer";
                    recModel = "PP-OCRv6_small_rec_infer";
                }
                else if (model == "PP-OCRv6_tiny" && tinyExists)
                {
                    detModel = "PP-OCRv6_tiny_det_infer";
                    recModel = "PP-OCRv6_tiny_rec_infer";
                }
                else
                {
                    // Fallback to whichever is available if preferred model is missing
                    if (tinyExists)
                    {
                        detModel = "PP-OCRv6_tiny_det_infer";
                        recModel = "PP-OCRv6_tiny_rec_infer";
                    }
                    else if (smallExists)
                    {
                        detModel = "PP-OCRv6_small_det_infer";
                        recModel = "PP-OCRv6_small_rec_infer";
                    }
                }

                OCRModelConfig config = new OCRModelConfig
                {
                    det_infer = Path.Combine(libsDir, "inference", detModel),
                    cls_infer = Path.Combine(libsDir, "inference", "PP-OCRv5_mobile_cls_infer"),
                    rec_infer = Path.Combine(libsDir, "inference", recModel),
                    keys = Path.Combine(libsDir, "inference", "ppocr_keys.txt")
                };

                var engine = new PaddleOCREngine(config, ocrParam);

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

            // Ensure the selected model engine has loaded in the background
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
                        // Run OCR directly on the selected model
                        var ocrResult = engine.DetectText(bitmap);
                        if (ocrResult != null)
                        {
                            string text = string.Empty;
                            if (ocrResult.TextBlocks != null && ocrResult.TextBlocks.Count > 0)
                            {
                                var lines = new List<List<TextBlock>>();
                                var sortedByY = ocrResult.TextBlocks
                                    .OrderBy(b => b.BoxPoints != null && b.BoxPoints.Count > 0 ? b.BoxPoints[0].Y : 0)
                                    .ToList();

                                foreach (var block in sortedByY)
                                {
                                    if (block.BoxPoints == null || block.BoxPoints.Count == 0) continue;

                                    double blockY = block.BoxPoints[0].Y;
                                    double blockHeight = Math.Abs(block.BoxPoints[2].Y - block.BoxPoints[0].Y);
                                    if (blockHeight == 0) blockHeight = 15;

                                    bool added = false;
                                    foreach (var line in lines)
                                    {
                                        double lineY = line.Average(b => b.BoxPoints[0].Y);
                                        if (Math.Abs(blockY - lineY) < blockHeight * 0.5)
                                        {
                                            line.Add(block);
                                            added = true;
                                            break;
                                        }
                                    }

                                    if (!added)
                                    {
                                        lines.Add(new List<TextBlock> { block });
                                    }
                                }

                                var sortedLines = lines
                                    .OrderBy(l => l.Average(b => b.BoxPoints[0].Y))
                                    .Select(l => l.OrderBy(b => b.BoxPoints[0].X).ToList())
                                    .ToList();

                                StringBuilder sb = new StringBuilder();
                                foreach (var line in sortedLines)
                                {
                                    StringBuilder lineBuilder = new StringBuilder();
                                    for (int i = 0; i < line.Count; i++)
                                    {
                                        var block = line[i];
                                        if (i > 0)
                                        {
                                            string prev = line[i - 1].Text;
                                            string curr = block.Text;
                                            bool needSpace = false;
                                            if (!string.IsNullOrEmpty(prev) && !string.IsNullOrEmpty(curr))
                                            {
                                                char lastChar = prev[prev.Length - 1];
                                                char firstChar = curr[0];
                                                if (((lastChar >= 'a' && lastChar <= 'z') || (lastChar >= 'A' && lastChar <= 'Z') || (lastChar >= '0' && lastChar <= '9')) &&
                                                    ((firstChar >= 'a' && firstChar <= 'z') || (firstChar >= 'A' && firstChar <= 'Z') || (firstChar >= '0' && firstChar <= '9')))
                                                {
                                                    needSpace = true;
                                                }
                                            }
                                            if (needSpace)
                                            {
                                                lineBuilder.Append(" ");
                                            }
                                        }
                                        lineBuilder.Append(block.Text);
                                    }
                                    sb.AppendLine(lineBuilder.ToString());
                                }
                                text = sb.ToString().Trim();
                            }
                            else
                            {
                                text = ocrResult.Text?.Trim() ?? string.Empty;
                            }

                            if (ocrResult is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                            return text;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("OCR 识别发生错误:\n" + ex.Message, "OCR 错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    // Reset timer again after OCR completes to start the 5s countdown
                    ResetDisposeTimer();
                }
                return string.Empty;
            });

            // Immediately run memory optimization and working set trim after OCR completes
            OptimizeMemory();

            return result;
        }

        public static void FreeMklBuffers()
        {
            try
            {
                MKL_Free_Buffers();
            }
            catch
            {
                try
                {
                    mkl_free_buffers();
                }
                catch { }
            }
        }

        public static void OptimizeMemory()
        {
            try
            {
                // Force .NET garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Free Intel MKL internal thread-local scratch buffers to drop background RAM usage
                FreeMklBuffers();

                // Trim physical memory pages back to OS standby list
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                EmptyWorkingSet(process.Handle);
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
            // Free Intel MKL internal buffers upon engine disposal
            FreeMklBuffers();
        }

        private static void ResetDisposeTimer()
        {
            lock (_lock)
            {
                if (_disposeTimer == null)
                {
                    _disposeTimer = new System.Threading.Timer(OnDisposeTimerFired, null, 300000, System.Threading.Timeout.Infinite);
                }
                else
                {
                    _disposeTimer.Change(300000, System.Threading.Timeout.Infinite);
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
