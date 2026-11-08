# LAST LINE

Unity 6 / C#で作る、9:16の3D防衛シューティングの最小プロトタイプです。プリミティブによるオリジナルの表現とUnity標準機能を使い、有料アセット・外部ゲームのコードは使用しません。

## 現在の状態

**Unity 6のプロジェクトへ取り込むソースリポジトリです。まだHubでそのまま開ける完全なUnityプロジェクトではありません。** 実際の動作確認プロジェクトはUnity 6000.6.8f1です。このリポジトリにはまだPackagesやProjectSettingsを取り込んでいません。

正面への自動射撃、マウス/タッチによる射手の横移動、敵のHP・移動・撃破、防衛ラインとライフ、60秒クリア、得点、再試行、敵/弾プール、シーン生成メニューを実装しています。敵の出現間隔は0〜20秒が0.65秒、20〜40秒が0.3秒、40〜60秒が0.1秒に変化します。マズルフラッシュとプール式の撃破演出も追加しています。

純C#の49テストと全13ファイルの構文検査は成功しています。固定砲台版では敵の増加・演出・Windows exeでのプレイをユーザーが確認済みです。今回、固定砲台から横移動する射手へ変更しました。Unity 6000.6.8f1の参照によるRuntime/Editorの独立コンパイルも成功しています。横移動版の操作感・見た目・Windows再ビルド・Android実機は未確認です。[操作と設定](docs/MOVEMENT.md)、[演出](docs/EFFECTS.md)、[敵増加](docs/WAVES.md) を参照してください。

## フォルダー構成

```text
Assets/LastLine/     ゲーム本体とEditor拡張（.metaを含む）
docs/SETUP.md       Unityへの導入・操作・Inspector調整・ビルド手順
docs/VALIDATION.md  実施済みと未確認の検証範囲
docs/validation/   実行結果ログ
docs/GIT.md        初回コミット・GitHub接続・Unityプロジェクト化の手順
tools/             Unityなしで実行できる検証スクリプト
.gitignore         Unityのキャッシュ・ビルド・個人設定の除外
.gitattributes     テキストの改行とバイナリの扱い
```

まず [導入手順](docs/SETUP.md) に従い、Unity 6の3DプロジェクトへAssetsを取り込み、**Last Line > Create Minimal Scene** を実行してください。`.meta`も一緒にコピーします。

## 検証（PowerShell 7）

リポジトリ直下で実行します。

```powershell
pwsh -NoProfile -File .\tools\Verify-Rules.ps1
pwsh -NoProfile -File .\tools\Verify-Syntax.ps1
# Unityを使った独立コンパイル手順はdocs/EFFECTS.mdを参照
```

Unity上では **Last Line > Run Edit Mode Checks / Run Play Mode Checks** を利用できます。詳細は [検証記録](docs/VALIDATION.md) に記載しています。

## Git管理

ローカルの保存先は `D:\GitHub\yusuke2721\LastLinePrototype`、ブランチは `main` です。接続先は公開リポジトリ [yusuke2721/LastLinePrototype](https://github.com/yusuke2721/LastLinePrototype) です。

[Git操作手順](docs/GIT.md) に、今後のコミットとGitHubへのプッシュ手順を記載しています。`.meta`を削除・再生成せず、アセットとセットで管理してください。生成したシーン・Prefab・Materialも管理対象です。
