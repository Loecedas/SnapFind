using System;
using System.Net;

namespace SnapFind.Plugin.Translation
{
    /// <summary>
    /// 动态感知 Windows 注册表系统代理状态的网络代理
    /// 能够在系统代理开关或端口变动时无需重启应用实时生效
    /// </summary>
    public class DynamicSystemWebProxy : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public Uri? GetProxy(Uri destination)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
                if (key != null)
                {
                    object? proxyEnable = key.GetValue("ProxyEnable");
                    if (proxyEnable is int enableVal && enableVal == 1)
                    {
                        object? proxyServer = key.GetValue("ProxyServer");
                        if (proxyServer is string serverStr && !string.IsNullOrEmpty(serverStr))
                        {
                            string proxyAddress = serverStr;
                            if (serverStr.Contains(';'))
                            {
                                var parts = serverStr.Split(';');
                                foreach (var part in parts)
                                {
                                    if (part.StartsWith("https=", StringComparison.OrdinalIgnoreCase) && destination.Scheme == "https")
                                    {
                                        proxyAddress = part.Substring(6);
                                        break;
                                    }
                                    if (part.StartsWith("http=", StringComparison.OrdinalIgnoreCase) && destination.Scheme == "http")
                                    {
                                        proxyAddress = part.Substring(5);
                                        break;
                                    }
                                }
                            }

                            if (!proxyAddress.Contains("://"))
                            {
                                proxyAddress = "http://" + proxyAddress;
                            }

                            return new Uri(proxyAddress);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public bool IsBypassed(Uri destination)
        {
            return destination.IsLoopback;
        }
    }
}
