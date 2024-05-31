internal class Program
{
    private static async Task Main(string[] args)
    {
        // ホストの作成
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((_, services) => services.AddGitHubActionServices()) // GitHubアクションのサービスを追加
            .Build();

        // ホストからサービスを取得するためのメソッド
        static TService Get<TService>(IHost host) where TService : notnull =>
            host.Services.GetRequiredService<TService>();

        // 解析を開始するための非同期メソッド
        static async Task StartAnalysisAsync(ActionInputs inputs, IHost host)
        {
            // プロジェクトのワークスペースを取得
            using ProjectWorkspace workspace = Get<ProjectWorkspace>(host);

            // キャンセル用のトークンソースを作成
            using CancellationTokenSource tokenSource = new();

            // Ctrl+Cでキャンセルされた場合のハンドラーを追加
            Console.CancelKeyPress += delegate
            {
                tokenSource.Cancel();
            };

            // プロジェクトのメトリックデータ解析クラスを取得
            var projectAnalyzer = Get<ProjectMetricDataAnalyzer>(host);

            // マッチャーの作成とCS/VBプロジェクトのパターンを追加
            Matcher matcher = new();
            matcher.AddIncludePatterns(new[] { "**/*.csproj", "**/*.vbproj" });

            // メトリックデータを格納するディクショナリーを作成
            Dictionary<string, CodeAnalysisMetricData> metricData = new(StringComparer.OrdinalIgnoreCase);
            var projects = matcher.GetResultsInFullPath(inputs.Directory); // プロジェクトの一覧を取得

            // 各プロジェクトの解析を実行
            foreach (var project in projects)
            {
                var metrics =
                    await projectAnalyzer.AnalyzeAsync(
                        workspace, project, tokenSource.Token); // 解析を非同期で実行

                // メトリックデータをディクショナリーに追加
                foreach (var (path, metric) in metrics)
                {
                    metricData[path] = metric;
                }
            }

            var updatedMetrics = false;
            var title = "";
            StringBuilder summary = new();

            // メトリックデータがある場合
            if (metricData is { Count: > 0 })
            {
                // マークダウンファイルの作成
                var fileName = "CODE_METRICS.md";
                var fullPath = Path.Combine(inputs.Directory, fileName);
                var logger = Get<ILoggerFactory>(host).CreateLogger(nameof(StartAnalysisAsync));
                var fileExists = File.Exists(fullPath);

                logger.LogInformation(
                    $"{(fileExists ? "Updating" : "Creating")} {fileName} markdown file with latest code metric data.");

                summary.AppendLine(
                    title = $"{(fileExists ? "Updated" : "Created")} {fileName} file, analyzed metrics for {metricData.Count} projects.");

                // 各プロジェクトのパスを追加
                foreach (var (path, _) in metricData)
                {
                    summary.AppendLine($"- *{path}*");
                }

                // メトリックデータをマークダウンファイルに書き込む
                var contents = metricData.ToMarkDownBody(inputs);
                await File.WriteAllTextAsync(
                    fullPath,
                    contents,
                    tokenSource.Token);

                updatedMetrics = true;
            }
            else
            {
                summary.Append("No metrics were determined.");
            }

            // GitHub Actionsの出力に情報を送信
            var githubOutputFile = Environment.GetEnvironmentVariable("GITHUB_OUTPUT", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrWhiteSpace(githubOutputFile))
            {
                using (var textWriter = new StreamWriter(githubOutputFile!, true, Encoding.UTF8))
                {
                    textWriter.WriteLine($"updated-metrics={updatedMetrics}");
                    textWriter.WriteLine($"summary-title={title}");
                    textWriter.WriteLine("summary-details<<EOF");
                    textWriter.WriteLine(summary);
                    textWriter.WriteLine("EOF");
                }
            }
            else
            {
                Console.WriteLine($"::set-output name=updated-metrics::{updatedMetrics}");
                Console.WriteLine($"::set-output name=summary-title::{title}");
                Console.WriteLine($"::set-output name=summary-details::{summary}");
            }

            Environment.Exit(0); // プロセスを終了
        }

        // コマンドライン引数を解析して、解析メソッドを開始
        var parser = Default.ParseArguments<ActionInputs>(() => new(), args);
        parser.WithNotParsed(
            errors =>
            {
                // エラーがある場合はログに出力
                Get<ILoggerFactory>(host)
                    .CreateLogger("DotNet.GitHubAction.Program")
                    .LogError(
                        string.Join(Environment.NewLine, errors.Select(error => error.ToString())));

                Environment.Exit(2); // エラーコード2で終了
            });

        await parser.WithParsedAsync(options => StartAnalysisAsync(options, host)); // 解析を開始
        await host.RunAsync(); // ホストを非同期で実行
    }
}
