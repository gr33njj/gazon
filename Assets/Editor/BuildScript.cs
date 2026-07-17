using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Gazon.EditorTools
{
    /// <summary>
    /// WebGL-сборка для превью в браузере (см. Docs/Architecture.md, «Превью билда прямо в браузере»).
    /// Отдельно от релизной Steam-сборки (Windows/Mac/Linux) — та собирается вручную по явному запросу,
    /// не через этот скрипт и не при каждой задаче.
    /// Запуск из редактора: Gazon → Собрать WebGL-превью.
    /// Запуск из CI (self-hosted runner, см. .github/workflows/): через -executeMethod, см. ниже BuildWebGLCI.
    /// </summary>
    public static class BuildScript
    {
        private const string OutputPath = "Build/WebGL";

        [MenuItem("Gazon/Собрать WebGL-превью")]
        public static void BuildWebGL()
        {
            var result = Build();
            if (result != BuildResult.Succeeded)
                Debug.LogError($"WebGL-сборка не удалась: {result}");
            else
                Debug.Log($"WebGL-сборка готова: {OutputPath}");
        }

        /// <summary>Точка входа для батч-режима (GitHub Actions runner) — завершает процесс с кодом
        /// возврата, чтобы CI понимал, провалилась сборка или нет.</summary>
        public static void BuildWebGLCI()
        {
            var result = Build();
            EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1);
        }

        private static BuildResult Build()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            return report.summary.result;
        }
    }
}
