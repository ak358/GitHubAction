namespace DotNet.GitHubAction;

// GitHubアクションの入力を処理するクラス
public class ActionInputs
{
    // リポジトリ名とブランチ名を保持するプライベートフィールド //変更
    string _repositoryName = null!;
    string _branchName = null!;

    // コンストラクタ
    public ActionInputs()
    {
        // GREETINGS環境変数が設定されている場合、その値をコンソールに出力する
        if (Environment.GetEnvironmentVariable(
            "GREETINGS") is { Length: > 0 } greetings)
        {
            Console.WriteLine(greetings);
        }
    }

    // オプション「owner」：リポジトリの所有者を指定する
    [Option('o', "owner",
        Required = true,
        HelpText = "The owner, for example: \"dotnet\". Assign from `github.repository_owner`.")]
    public string Owner { get; set; } = null!;

    // オプション「name」：リポジトリ名を指定する
    [Option('n', "name",
        Required = true,
        HelpText = "The repository name, for example: \"samples\". Assign from `github.repository`.")]
    public string Name
    {
        // プロパティ「Name」のゲッターとセッター
        get => _repositoryName;
        set => ParseAndAssign(value, str => _repositoryName = str);
    }

    // オプション「branch」：ブランチ名を指定する
    [Option('b', "branch",
        Required = true,
        HelpText = "The branch name, for example: \"refs/heads/main\". Assign from `github.ref`.")]
    public string Branch
    {
        // プロパティ「Branch」のゲッターとセッター
        get => _branchName;
        set => ParseAndAssign(value, str => _branchName = str);
    }

    // オプション「dir」：再帰的な検索を開始するルートディレクトリを指定する
    [Option('d', "dir",
        Required = true,
        HelpText = "The root directory to start recursive searching from.")]
    public string Directory { get; set; } = null!;

    // オプション「workspace」：ワークスペースディレクトリまたはリポジトリのルートディレクトリを指定する
    [Option('w', "workspace",
        Required = true,
        HelpText = "The workspace directory, or repository root directory.")]
    public string WorkspaceDirectory { get; set; } = null!;

    // 文字列をパースして割り当てる補助メソッド
    static void ParseAndAssign(string? value, Action<string> assign)
    {
        // 文字列が空でない場合、割り当てを行う
        if (value is { Length: > 0 } && assign is not null)
        {
            assign(value.Split("/")[^1]);
        }
    }
}
