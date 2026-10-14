using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;

#if USE_RAPID_OCR
using RapidOcrNet;
using SkiaSharp;
#else
using PaddleOCRSharp;
#endif

namespace PixOcrSearch
{
    public static class OcrHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("psapi.dll", EntryPoint = "EmptyWorkingSet", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

#if !USE_RAPID_OCR
        [DllImport("mklml.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "MKL_Free_Buffers", CharSet = CharSet.Ansi)]
        private static extern void MKL_Free_Buffers();

        [DllImport("mklml.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "mkl_free_buffers", CharSet = CharSet.Ansi)]
        private static extern void mkl_free_buffers();

        private static PaddleOCREngine? _engine;
#else
        private static RapidOcr? _engine;
#endif

        private static Task? _initTask;
        private static System.Threading.Timer? _disposeTimer;
        private static string _currentModel = "";
        // _lock guards the engine/timer/initTask state for short-lived mutations.
        private static readonly object _lock = new object();
        // _syncLock serializes long-lived inference operations against engine disposal/replacement
        // so that an in-flight OCR inference cannot collide with a model swap or timer-triggered
        // disposal (which previously caused ObjectDisposedException / NullReferenceException crashes).
        private static readonly object _syncLock = new object();
        // Tracks the number of in-flight OCR inference operations so that Dispose/StartInitialize
        // can wait for all current users of the engine to finish before tearing it down.
        private static int _inFlightCount = 0;

        static OcrHelper()
        {
            try
            {
#if USE_RAPID_OCR
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string libsRapidDir = Path.Combine(baseDir, "libs", "rapid");
                if (!Directory.Exists(libsRapidDir))
                {
                    string c = Path.Combine(baseDir, "..", "..", "..", "libs", "rapid");
                    if (Directory.Exists(c)) libsRapidDir = Path.GetFullPath(c);
                    else
                    {
                        string c2 = Path.Combine(baseDir, "..", "..", "..", "..", "libs", "rapid");
                        if (Directory.Exists(c2)) libsRapidDir = Path.GetFullPath(c2);
                    }
                }
                if (Directory.Exists(libsRapidDir))
                {
                    SetDllDirectory(libsRapidDir);
                }
#else
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string libsDir = Path.Combine(baseDir, "libs");
                if (!Directory.Exists(libsDir))
                {
                    string c = Path.Combine(baseDir, "..", "..", "..", "libs");
                    if (Directory.Exists(c)) libsDir = Path.GetFullPath(c);
                }
                if (Directory.Exists(libsDir))
                {
                    SetDllDirectory(libsDir);
                }
#endif
            }
            catch { }
        }

        // Start initialization asynchronously in the background using the user-selected model
        public static void StartInitialize()
        {
            string modelToLoad = ConfigManager.Current.OcrModel;
            IDisposable? engineToDispose = null;
            Task? newInitTask = null;
            lock (_lock)
            {
                if (_engine == null || _currentModel != modelToLoad)
                {
                    // If model is changing, dispose old engine after all in-flight inferences
                    // have finished to avoid ObjectDisposedException / native crashes.
                    engineToDispose = _engine as IDisposable;
                    _engine = null;
                    _currentModel = modelToLoad;
                    newInitTask = Task.Run(() => InitializeInternal(modelToLoad));
                    _initTask = newInitTask;
                }
            }

            // Defer disposing the previous engine until no caller is using it anymore.
            if (engineToDispose != null)
            {
                DisposeEngineWhenIdle(engineToDispose);
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
#if USE_RAPID_OCR
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string libsRapidDir = Path.Combine(baseDir, "libs", "rapid");
                if (!Directory.Exists(libsRapidDir))
                {
                    string c = Path.Combine(baseDir, "..", "..", "..", "libs", "rapid");
                    if (Directory.Exists(c)) libsRapidDir = Path.GetFullPath(c);
                    else
                    {
                        string c2 = Path.Combine(baseDir, "..", "..", "..", "..", "libs", "rapid");
                        if (Directory.Exists(c2)) libsRapidDir = Path.GetFullPath(c2);
                    }
                }
                if (Directory.Exists(libsRapidDir))
                {
                    SetDllDirectory(libsRapidDir);
                }

                string modelsDir = Path.Combine(libsRapidDir, "rapid_models");
                if (!Directory.Exists(modelsDir))
                {
                    modelsDir = Path.Combine(baseDir, "libs", "rapid_models");
                }
                if (!Directory.Exists(modelsDir))
                {
                    modelsDir = Path.Combine(baseDir, "rapid_models");
                }
                if (!Directory.Exists(modelsDir))
                {
                    modelsDir = Path.Combine(baseDir, "libs");
                }

                string det = Path.Combine(modelsDir, "ch_PP-OCRv4_det_mobile.onnx");
                string cls = Path.Combine(modelsDir, "ch_ppocr_mobile_v2.0_cls_mobile.onnx");
                string rec = Path.Combine(modelsDir, "ch_PP-OCRv4_rec_mobile.onnx");
                string keys = Path.Combine(modelsDir, "ppocr_keys.txt");

                if (!File.Exists(keys))
                {
                    string fallbackKeys = Path.Combine(baseDir, "libs", "inference", "ppocr_keys.txt");
                    if (File.Exists(fallbackKeys)) keys = fallbackKeys;
                }

                var engine = new RapidOcr();
                int threads = Math.Min(4, Environment.ProcessorCount);
                engine.InitModels(det, cls, rec, keys, threads);

                lock (_lock)
                {
                    _engine = engine;
                }
#else
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
#endif
            }
            catch (Exception ex)
            {
                ShowErrorOnUiThread(Localization.MsgOcrInitFailed(ex.Message), Localization.TitleOcrError);
            }
        }

        public static async Task<string> RecognizeTextAsync(System.Drawing.Bitmap bitmap)
        {
            if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return string.Empty;
            }

            // Reset the inactivity timer when a screenshot is captured
            ResetDisposeTimer();

            // Ensure the selected model engine has loaded in the background
            await EnsureInitializedAsync();

            string result = await Task.Run(() =>
            {
                Interlocked.Increment(ref _inFlightCount);
                try
                {
                    // Hold _syncLock across the entire inference so a concurrent Dispose /
                    // engine-replacement triggered by the timer or a model change will wait
                    // for the inference to finish before tearing down the engine.
                    lock (_syncLock)
                    {
                        return RunOcrInference(bitmap);
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorOnUiThread(Localization.MsgOcrRecognitionFailed(ex.Message), Localization.TitleOcrError);
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlightCount);
                    // Reset timer again after OCR completes to start the countdown
                    ResetDisposeTimer();
                }
                return string.Empty;
            });

            // Immediately run memory optimization and working set trim after OCR completes
            OptimizeMemory();

            return result;
        }

        // Runs the actual OCR inference against the engine currently held by _engine.
        // MUST be called from inside a lock (_syncLock) block so that the engine is not
        // disposed or replaced mid-call.
        private static string RunOcrInference(System.Drawing.Bitmap bitmap)
        {
#if USE_RAPID_OCR
            RapidOcr? engine;
            lock (_lock)
            {
                engine = _engine;
            }

            if (engine == null)
            {
                return string.Empty;
            }

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            using var skBmp = SKBitmap.Decode(ms);

            if (skBmp == null)
            {
                return string.Empty;
            }

            var ocrResult = engine.Detect(skBmp, RapidOcrOptions.Default, CancellationToken.None);
            if (ocrResult != null && ocrResult.TextBlocks != null && ocrResult.TextBlocks.Length > 0)
            {
                var lines = new List<List<RapidOcrNet.TextBlock>>();
                var sortedByY = ocrResult.TextBlocks
                    .OrderBy(b => b.BoxPoints != null && b.BoxPoints.Length > 0 ? b.BoxPoints[0].Y : 0)
                    .ToList();

                foreach (var block in sortedByY)
                {
                    if (block.BoxPoints == null || block.BoxPoints.Length < 4) continue;

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
                        lines.Add(new List<RapidOcrNet.TextBlock> { block });
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
                return sb.ToString().Trim();
            }
            else if (ocrResult != null && !string.IsNullOrEmpty(ocrResult.StrRes))
            {
                return ocrResult.StrRes.Trim();
            }
            return string.Empty;
#else
            PaddleOCREngine? engine;
            lock (_lock)
            {
                engine = _engine;
            }

            if (engine == null)
            {
                return string.Empty;
            }

            // Run OCR directly on the selected model
            var ocrResult = engine.DetectText(bitmap);
            if (ocrResult == null)
            {
                return string.Empty;
            }

            string text = string.Empty;
            try
            {
                if (ocrResult.TextBlocks != null && ocrResult.TextBlocks.Count > 0)
                {
                    var lines = new List<List<PaddleOCRSharp.TextBlock>>();
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
                            lines.Add(new List<PaddleOCRSharp.TextBlock> { block });
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
            }
            finally
            {
                if (ocrResult is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            return text;
#endif
        }

#if !USE_RAPID_OCR
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
#endif

        public static void OptimizeMemory()
        {
            Task.Run(() =>
            {
                try
                {
                    // Force .NET garbage collection
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

#if !USE_RAPID_OCR
                    // Free Intel MKL internal thread-local scratch buffers to drop background RAM usage
                    FreeMklBuffers();
#endif

                    // Trim physical memory pages back to OS standby list
                    using var process = System.Diagnostics.Process.GetCurrentProcess();
                    EmptyWorkingSet(process.Handle);
                }
                catch { }
            });
        }

        public static void Dispose()
        {
            IDisposable? engineToDispose = null;
            lock (_lock)
            {
                if (_disposeTimer != null)
                {
                    _disposeTimer.Dispose();
                    _disposeTimer = null;
                }
                if (_engine != null)
                {
                    engineToDispose = _engine as IDisposable;
                    _engine = null;
                }
                _initTask = null;
            }

            // Defer the actual engine release until any in-flight inference completes so
            // we never tear the engine down while RecognizeTextAsync is still using it.
            if (engineToDispose != null)
            {
                DisposeEngineWhenIdle(engineToDispose);
            }
#if !USE_RAPID_OCR
            // Free Intel MKL internal buffers upon engine disposal
            FreeMklBuffers();
#endif
        }

        // Wait until no inference is in flight (or a short timeout elapses) and then dispose
        // the supplied engine. This avoids ObjectDisposedException when a model swap or the
        // 5-minute timer fires while a screenshot's OCR is still running.
        private static void DisposeEngineWhenIdle(IDisposable engine)
        {
            if (engine == null) return;

            Task.Run(() =>
            {
                try
                {
                    // Acquire _syncLock and immediately release it to ensure all currently
                    // running inferences have exited, then dispose the engine afterwards.
                    lock (_syncLock)
                    {
                        // The lock is held; no other inference can be running.
                    }

                    // Also wait for the in-flight counter to drain (the inference thread may
                    // have incremented the counter just before the lock was released).
                    SpinWait sw = new SpinWait();
                    while (Volatile.Read(ref _inFlightCount) > 0)
                    {
                        sw.SpinOnce();
                        if (sw.Count > 200)
                        {
                            Thread.Sleep(1);
                        }
                    }

                    try
                    {
                        engine.Dispose();
                    }
                    catch
                    {
                        // Suppress disposal errors to avoid crashing timer-driven cleanup.
                    }
                }
                catch
                {
                    // Defensive: never let this background path throw out of Task.Run.
                }
            });
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

        // Dispatch an error dialog to the WPF UI thread. Calling MessageBox.Show from a
        // background thread (e.g. when OCR init/recognition fails inside Task.Run) throws
        // InvalidOperationException because there is no interactive user on that thread.
        private static void ShowErrorOnUiThread(string message, string title)
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app == null)
                {
                    ModernDialog.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }
                if (app.Dispatcher.CheckAccess())
                {
                    ModernDialog.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                else
                {
                    app.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ModernDialog.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }));
                }
            }
            catch
            {
                // Never let notification failures crash the OCR pipeline.
            }
        }
    }
}