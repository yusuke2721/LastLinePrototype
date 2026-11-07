# Git / GitHubでの管理

## 接続先

- ローカル: `D:\GitHub\yusuke2721\LastLinePrototype`
- GitHub: [yusuke2721/LastLinePrototype](https://github.com/yusuke2721/LastLinePrototype)
- 公開範囲: Public（ユーザー指定）
- リモート: `origin` / `https://github.com/yusuke2721/LastLinePrototype.git`
- ブランチ: `main`
- Gitの作成者設定はこのリポジトリだけに適用。名前は `yusuke2721`、メールはGitHub IDに基づくnoreply形式です。
- ZIPは使用せず、ソース・資料・検証スクリプトを管理します。

GitHubへの保存はPC上のGitから行います。CodexのGitHub連携と、PC上のGit Credential Managerの認証は別です。通常の `git push` で認証を求められた場合は、GitHubの案内に従って自分のブラウザーで認証してください。パスワードやトークンをチャットに貼る必要はありません。

## 今後のコミットとプッシュ

PowerShellで以下を実行します。差分を確認してから、追加したいファイルをステージします。

```powershell
Set-Location D:\GitHub\yusuke2721\LastLinePrototype
git status
git diff
git add Assets docs tools README.md .gitignore .gitattributes
git diff --cached --stat
git diff --cached --check
git commit -m "Describe your change"
git push
```

`Packages` と `ProjectSettings` を追加した後は、それらもステージしてください。ステージ後に再編集した場合は、コミット前にもう一度対象ファイルを `git add` します。

接続と同期の確認:

```powershell
git remote -v
git status --short --branch
git log -3 --oneline
```

新しいPCでは `git clone https://github.com/yusuke2721/LastLinePrototype.git` で取得できます。現在はソース一式なので、Unityへの取り込み方法は [SETUP.md](SETUP.md) を参照してください。

## 完全なUnityプロジェクトとして管理する段階

今はソース一式です。実際に使用するUnity 6のバージョンを確認してから、次のように同じリポジトリを完全なプロジェクトにできます。

1. Unity Hubで、リポジトリの外にUniversal 3DまたはBuilt-in 3Dの空プロジェクトを作成し、一度開いてからUnityを閉じます。
2. そのプロジェクトの `Packages` と `ProjectSettings` を、このリポジトリの直下へコピーします。`ProjectSettings/ProjectVersion.txt` に実際のUnityバージョンが記録されます。
3. テンプレートの `Assets` 内にある設定・素材も対応する `.meta` と一緒にリポジトリの `Assets` へコピーします。URPではRendererやRender Pipelineの設定アセットも必要です。既存の `Assets/LastLine` とその `.meta` は保持してください。
4. Hubの既存プロジェクト追加機能から `D:\GitHub\yusuke2721\LastLinePrototype` を追加して開き、インポート完了を待ちます。
5. Project SettingsでVersion ControlをVisible Meta Files、Asset SerializationをForce Textに設定します。表示位置はUnityのバージョンによって異なります。
6. シーン生成・検証・Playを実行し、`Assets`、`Packages`、`ProjectSettings`、関連 `.meta` を一緒にコミットします。

`Library`、`Temp`、`Obj`、`Logs`、`UserSettings`、ビルド出力やIDEの個人設定は `.gitignore` で除外します。`.meta` はGUIDによる参照を保つため必ずアセットとセットで管理します。`Assets/LastLine/Generated` のシーン・Prefab・Materialは生成後に調整する成果物なので、除外していません。

参考: [Unityのプロジェクトディレクトリ](https://docs.unity.com/en-us/engine/6000.0/manual/get-started/project-configuration/default-directories)、[Unityのアセットメタデータ](https://docs.unity.com/en-us/engine/6000.3/manual/assets-and-media/import-assets/asset-metadata)。
