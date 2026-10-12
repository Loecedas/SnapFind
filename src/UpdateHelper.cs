using System;
using System.Collections.Generic;

namespace PixOcrSearch
{
    public static class UpdateHelper
    {
        /// <summary>
        /// 根据当前安装类型与引擎版本，在 release assets 列表中选出最匹配的更新包名称
        /// </summary>
        public static string? SelectAsset(
            IEnumerable<string> assets,
            string currentEdition,
            bool isInstalled)
        {
            string oppositeEdition = currentEdition.Equals("Rapid", StringComparison.OrdinalIgnoreCase) ? "Paddle" : "Rapid";

            if (isInstalled)
            {
                // 安装版：匹配 SnapFindSetup_{Edition}_*.exe（排除 .7z.exe 自解压便携包）
                string targetPattern = $"SnapFindSetup_{currentEdition}_";
                foreach (var name in assets)
                {
                    if (name.StartsWith(targetPattern, StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        !name.EndsWith(".7z.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                }

                // 回退兼容历史无 Edition 标识的安装包
                string legacyPattern = "SnapFindSetup_";
                foreach (var name in assets)
                {
                    if (name.StartsWith(legacyPattern, StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        !name.EndsWith(".7z.exe", StringComparison.OrdinalIgnoreCase) &&
                        !name.Contains(oppositeEdition, StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                }

                return null;
            }
            else
            {
                // 便携版（Umi-OCR 风格）：
                // 1. 优先匹配当前版本的 .7z.exe 自解压程序
                string portablePrefix = $"SnapFindPortable_{currentEdition}_";
                string shortPrefix = $"SnapFind_{currentEdition}_";
                foreach (var name in assets)
                {
                    if ((name.StartsWith(portablePrefix, StringComparison.OrdinalIgnoreCase) ||
                         name.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase)) &&
                        name.EndsWith(".7z.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                }

                // 2. 匹配当前版本的 .7z 纯压缩包
                foreach (var name in assets)
                {
                    if ((name.StartsWith(portablePrefix, StringComparison.OrdinalIgnoreCase) ||
                         name.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase)) &&
                        name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                }

                // 3. 兼容历史 .zip 包
                foreach (var name in assets)
                {
                    if ((name.StartsWith(portablePrefix, StringComparison.OrdinalIgnoreCase) ||
                         name.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase)) &&
                        name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                }

                // 3. 回退兼容无 Edition 标识的历史便携包（优先 .7z.exe，后备 .zip）
                foreach (var name in assets)
                {
                    if (name.StartsWith("SnapFindPortable_", StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".7z.exe", StringComparison.OrdinalIgnoreCase) &&
                        !name.Contains(oppositeEdition, StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                }

                foreach (var name in assets)
                {
                    if (name.StartsWith("SnapFindPortable_", StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                        !name.Contains(oppositeEdition, StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                }

                return null;
            }
        }
    }
}
