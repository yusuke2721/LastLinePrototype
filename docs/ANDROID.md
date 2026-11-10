# 知人配布用のAndroid APK

2026-11-10に0.1.9のAPKを生成しました。保存先はリポジトリの `Builds/Android/LastLine-0.1.9-arm64.apk`（30,073,359バイト、約30MB）です。同じフォルダーにインストール方法、ビルド情報、SHA-256チェックサムを置いています。APKの署名（v2）とzipalign検証は成功しています。Android実機でのインストールとプレイは未確認です。

Unity 6000.6.8f1にAndroid Build Support、Android SDK & NDK Tools、OpenJDKが必要です。Unity Hubのモジュール管理で追加できます。

今回のPCではHubの昇格処理が進まなかったため、署名確認済みのUnity公式Androidインストーラーを直接起動しました。SDK・NDK・JavaはHubがダウンロードしたアーカイブから `Builds/AndroidTools` へ配置しています。`tools/Setup-AndroidTools.ps1` で再配置でき、ビルド時に環境変数 `LASTLINE_ANDROID_TOOLS` をこのフォルダーへ設定すると、UnityのExternal Tools設定へSDK・NDK・JDKのパスを反映します。UnityのインストールフォルダーへのSDKの書き込みは不要です。

使用する構成はSDK Platform / Build Tools 36、NDK r27c、OpenJDK 17、CMake 3.22.1です。外部ツールの設定方法は [Unity公式API](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/android/androidexternaltoolssettings) に従っています。

## ビルド

1. Unityで最新スクリプトのインポートを完了し、生成済みのLastLineシーンを開きます。
2. Playを停止し、**Last Line > Build Android Test APK** を実行します。
3. プロジェクト内の `Builds/Android/LastLine-0.1.9-arm64.apk` に出力します。結果の概要は同じフォルダーの `build-info.txt` に記録します。

ビルドメニューはアプリ名をLAST LINE、パッケージ名を `com.yusuke2721.lastline`、バージョンを0.1.9（コード10）に設定します。Android 8.0以降のARM64端末向け、縦画面、IL2CPP、OpenGL ES 3、ETC2でビルドします。開いているシーンだけを含め、AABやOBBではなく単独APKを出力します。端末によって対応する描画機能や性能に差があります。

コマンドラインでは、同じプロジェクトを開いているUnity Editorを終了してから、`-batchmode -nographics -projectPath <Unityプロジェクト> -buildTarget Android -executeMethod LastLine.Editor.AndroidApkBuilder.BuildFromCommandLine -logFile <ログ保存先>` を利用できます。既存の `Assets/LastLine/Generated/Minimal/LastLine.unity` を使います。

## 配布とインストール

APKファイルを知人へ渡し、Android端末上で開いてインストールします。必要に応じて、そのファイルを開くアプリに対する「不明なアプリのインストール」を端末の案内に従って許可します。Playストア公開の手順とは異なります。32ビット専用端末やiPhoneにはインストールできません。

今回のビルドメニューは、Unityのローカルのデバッグ用鍵で署名するテスト配布用です。同じアプリを上書き更新するには、同じ署名鍵を使う必要があります。今後継続して配布・ストア公開する場合は、正式な署名鍵とバージョンコードの運用へ切り替えてください。署名鍵はAPKと一緒に配布しません。

## 実機で見る項目

- タイトルの表示と出撃、指の左右移動、増員・武器強化・ドローン要請。
- ライン突破時の人数半減、クリアと次ステージ、再試行。
- 縦画面、ノッチ周辺の表示、操作中の発熱・フレームレート。
- バックグラウンドへ移動して戻ったときの状態。

APK生成と署名検証だけでは、Android実機での動作確認済みとは扱いません。

検証結果は [validation/android-apk.txt](validation/android-apk.txt) を参照してください。初回ビルドで見つかったHUDのEditor専用GUIプロパティ指定は除去し、Android Playerのビルド成功を確認しています。

## 0.1.1 起動不具合の対策

0.1.0では実機で初期砲台だけが表示され、タイトルへ進まない報告がありました。生成済みAndroidネイティブ登録コードからSphereColliderとCapsuleColliderが欠落していることを確認しました。兵士生成の先頭でCapsuleを生成し、そのColliderへアクセスするため、この削除は起動停止を起こす経路になります。

`Assets/LastLine/link.xml`でCreatePrimitiveに必要なMeshFilter・MeshRenderer・BoxCollider・SphereCollider・CapsuleColliderを保持します。参照: https://docs.unity3d.com/ja/6000.0/ScriptReference/GameObject.CreatePrimitive.html

HUDを先に接続し、初期化例外をログと画面に表示します。実機はADB未接続のため、端末ログによる原因確定および修正版のタッチ操作確認は未実施です。同じ署名のコード2なので0.1.0へ上書き更新できます。

## 0.1.2 戦車の表示確認

初戦で戦車の頭上に防衛線到達までの秒数（LINE）が表示され、残り5秒で赤になること、命中すると装甲表示が白く光ることを確認します。0.1.1と署名証明書が一致し、v2署名と16KB zipalign検証が成功しました。ADBの接続一覧は空で、実機更新・プレイは未確認です。

## 0.1.7 バランス調整の検証

UnityのEdit Mode・Play Mode検証が成功しました。5人・武器Lv.4でクリアし、次面への進行、タイトルからの再開、敗北後の再試行で3人・Lv.2になることを確認しました。APKのv2署名と16KBアラインメントを検証済みです。ADBに接続された端末がなく、実機の難易度は未確認です。
