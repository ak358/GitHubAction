using System.Collections.Immutable;

namespace DotNet.GitHubAction.Analyzers;

// ILoggerを使用するための名前空間
sealed class ProjectMetricDataAnalyzer
{
    readonly ILogger<ProjectMetricDataAnalyzer> _logger; // ILoggerインスタンスの宣言

    // コンストラクター
    public ProjectMetricDataAnalyzer(ILogger<ProjectMetricDataAnalyzer> logger) => _logger = logger;

    // メインの解析メソッド
    internal async Task<ImmutableArray<(string, CodeAnalysisMetricData)>> AnalyzeAsync(
        ProjectWorkspace workspace, string path, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested(); // キャンセルされた場合は例外をスロー

        if (File.Exists(path)) // ファイルが存在するかどうかを確認
        {
            _logger.LogInformation($"Computing analytics on {path}."); // ログ情報を出力
        }
        else
        {
            _logger.LogWarning($"{path} doesn't exist."); // ファイルが存在しない場合の警告を出力
            return ImmutableArray<(string, CodeAnalysisMetricData)>.Empty; // 空の結果を返す
        }

        // プロジェクトを読み込む
        var projects =
            await workspace.LoadProjectAsync(
                path, cancellationToken: cancellation)
                .ConfigureAwait(false);

        var builder = ImmutableArray.CreateBuilder<(string, CodeAnalysisMetricData)>(); // 結果のビルダーを作成
        foreach (var project in projects)
        {
            var compilation =
                await project.GetCompilationAsync(cancellation) // コンパイルを非同期で取得
                    .ConfigureAwait(false);

            // コンパイルされたアセンブリのメトリックデータを計算
            var metricData = await CodeAnalysisMetricData.ComputeAsync(
                compilation!.Assembly, // コンパイルされたアセンブリ
                new CodeMetricsAnalysisContext(compilation, cancellation)) // コードメトリクス解析コンテキスト
                    .ConfigureAwait(false);

            builder.Add((project.FilePath!, metricData)); // 結果をビルダーに追加
        }

        return builder.ToImmutable(); // 不変な結果を返す
    }
}
