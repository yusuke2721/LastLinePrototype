# LAST LINE — Unity 6 最小プロトタイプ

オリジナルのプリミティブを使った縦画面3D防衛シューティングです。無料のUnity標準機能だけで構成しています。このフォルダーは**既存のUnityプロジェクトではなく、Unity 6のプロジェクトへコピーするソース一式**です。

## 今回の到達点

- 左右に移動するマシンガン射手、斜め上からのカメラ、9:16の表示領域。
- マウスの左右位置で射手がステップし、正面へ毎秒2発の自動射撃。無限弾数。
- タッチ中の指の左右位置による射手の横移動。新旧のInput方式をコンパイル条件で切り替え。
- Capsuleの敵、個別HP、前進、命中による撃破、スコア。
- 防衛ライン突破でライフ減少、ライフ0で敗北、60秒生存でクリア。
- スコア・ライフ・経過時間、結果表示、RETRY。
- 敵100体・弾32発の事前生成プール。敵100体は上限であり、初期設定では同時に100体出現しません。
- メニューからシーン生成、検証、Windowsビルド。

初版の動作確認後、敵の出現を3段階に増やす処理を追加しました（0〜20秒: 0.65秒間隔、20〜40秒: 0.3秒間隔、40〜60秒: 0.1秒間隔）。**マズルフラッシュと撃破エフェクトも追加済み**です。効果音とUIの仕上げは次の段階です。演出の詳細は [EFFECTS.md](EFFECTS.md) を参照してください。射撃時の `On Shot` UnityEventは用意済みです。

## 最初にUnityで行う操作

1. Unity HubでUnity 6のインストール済みバージョンを確認します。未インストールならUnity 6を追加し、Windows向けビルドに必要なモジュールも追加します。初版の動作確認時にUnity 6000.6.8f1を使用していることを確認しました。
2. Hubの **New project** から **Universal 3D** または **3D (Built-In Render Pipeline)** の空プロジェクトを作成します。2D/HDRPは今回の対象外です。プロジェクト名の例は `LastLine`。
3. リポジトリ直下の **`Assets/LastLine` フォルダー全体と `Assets/LastLine.meta`**を、作成したプロジェクトの `Assets` へコピーします。`Assets/Assets` にならないようにしてください。既存プロジェクトに入れる場合も同じです。
4. Unityに戻り、右下のインポート・コンパイル完了を待ちます。Consoleに赤いエラーがないことを確認します。
5. 上部メニュー **Last Line > Create Minimal Scene** をクリックします。開いていたシーンの変更を保存する確認が出た場合は適切に保存してください。
6. `Assets/LastLine/Generated/Minimal/LastLine.unity` が生成されて開きます。再生成時は `Minimal 1` など別フォルダーを作るので、前の設定は残ります。
7. **Game** タブのアスペクト比メニューで **+** を選び、Typeを **Aspect Ratio**、Widthを **9**、Heightを **16**、Labelを `Portrait 9:16` にして追加・選択します。Gameタブがなければ **Window > General > Game** を開きます。実行時もカメラが9:16の領域を保ちます。
8. **Last Line > Run Edit Mode Checks** を実行し、Consoleの `PASS` を確認します。
9. **Play** を押し、Game画面内でマウスを左右に動かします。クリックしなくても射撃します。敵の列の正面へ移動してください。緑の線を敵が越えるとライフが減ります。
10. ゲームオーバーまたはクリア後に **RETRY** をクリックし、ライフ・スコア・時間・敵・弾がリセットされることを確認します。終了時はもう一度Playを押します。

New Input Systemだけが有効なプロジェクトでも利用できます。Legacyを有効にするために設定変更する必要はありません。横移動が反応しない場合は、Gameタブをクリックしてフォーカスし、**Edit > Project Settings > Player > Other Settings > Active Input Handling** がNew / Old / Bothのいずれかになっているか確認してください。NewはInput Systemパッケージが必要です。

## 自動検証と手動で見る項目

**Last Line > Run Play Mode Checks** は現在の生成シーンで自動的にPlayモードへ入り、初期化後に以下を検証してPlayモードを終了します。実行中はUnityを操作せず、Consoleの `PASS` またはエラーを確認します。対象は初期設定の生成シーンです。ランタイム設定変更は通常のPlayモード同様、終了時に戻ります（Enter Play Mode Optionsは標準設定を推奨）。

- プールからの出現、再利用時のHPリセット。
- 高速弾の経路判定、最も近い1体へのダメージ、HPが残る間の無得点。
- 撃破時の1回だけの加点、100体の上限。
- ライフ0、RETRYの初期化、制限時間でのクリア。

これはパッケージ不要のスモークチェックで、Test Runnerのテスト一覧には出ません。敵の実際の移動・画面配置・入力・RETRYボタンのクリック・2発/秒の見た目はPlayして確認してください。HPを2にした場合の2発で撃破も確認してください。

Unityに依存しない進行・命中判定は、PowerShell 7で次を実行すると、そのC#ソースを実際にコンパイルして49項目を検証できます。配布フォルダーをカレントディレクトリにして実行してください。PowerShell 7がない場合は、Unity内のチェックだけでも進められます。

```powershell
pwsh -NoProfile -File .\tools\Verify-Rules.ps1
```

実行ポリシーによりブロックされる場合は、組織のポリシーに従ってください。設定の変更は不要なUnity内のチェックを利用できます。

## ファイルと役割

```text
Assets/LastLine/Scripts/
  Runtime/
    CombatRules.cs       ライフ・得点・時間・勝敗と線分/円の判定（純C#）
    GameController.cs    初期化・ラウンド進行・再試行
    EnemySpawner.cs      敵の事前生成・出現・最も近い敵への命中判定
    EnemyAgent.cs        HP・前進・突破・プールへの返却
    MachineGun.cs        正面への2発/秒・弾プール・射撃イベント
    ShooterController.cs 左右移動・人型表示・ステップ動作・旧シーン互換
    Bullet.cs            弾の移動・寿命・経路による命中
    CombatEffects.cs     銃口の点滅とプール式の撃破演出
    PointerAim.cs        マウス/タッチ入力の取得
    PortraitCamera.cs    9:16のカメラ領域
    GameHud.cs           最小UI・結果・再試行ボタン
  Editor/
    MinimalSceneBuilder.cs  シーン・Prefab・Material生成、Windowsビルド
    PrototypeChecks.cs      Edit/Playモードの検証メニュー
tools/Verify-Rules.ps1    Unityを起動せずに純C#をコンパイル・検証
```

生成される構成:

```text
Main Camera  [Camera / AudioListener / PortraitCamera]
Sun          [Directional Light]
Battlefield  [床 / 防衛線 / 両側の目印]
GameRoot     [GameController / GameHud]
  Enemy Pool [EnemySpawner / 実行時に敵100体]
  Shooter    [MachineGun / ShooterController / Shooter Visuals / Muzzle]
  Bullet Pool [実行時に弾32発]
```

初期化とラウンド進行はGameController、敵移動は各EnemyAgent、その後にBulletが判定する順です。各弾はそのフレームの移動区間と敵の水平な円の交点を調べ、最も近い1体にだけ命中します。敵の高さ・弾の高さは固定で、上下照準・3D障害物は扱いません。Collider/Rigidbodyは不要です。被弾・寿命・突破・再試行時にプールへ戻します。戦闘中のInstantiate/Destroyはありません。

## Inspectorで調整する場所

Playを**停止してから**変更し、Ctrl+Sで保存してください。Play中だけの変更は停止時に戻ります。

| 対象 | 項目 | 初期値・意味 |
|---|---|---|
| GameRoot / GameController | Starting Lives | 10 |
| 同上 | Round Seconds | 60、到達フレームではクリアを優先 |
| Enemy Pool / EnemySpawner | Capacity | 100、初期化時のプール数 |
| 同上 | Increase Over Time | ONで時間に応じて増加、OFFで一定間隔 |
| 同上 | Spawn Interval | 序盤0.65秒 |
| 同上 | Middle Spawn Interval | 中盤0.3秒 |
| 同上 | Final Spawn Interval | 終盤0.1秒 |
| 同上 | Spawn Half Width / Spawn Z | ±5.5 / 25 |
| 同上 | Defense Z | 0（緑の線の見た目は別途移動が必要） |
| Shooter / MachineGun | Shots Per Second / Damage | 2 / 1 |
| Shooter / ShooterController | Move Half Width | 中央から左右5.5 |
| 同上 | Move Speed | 毎秒16、横移動の追従速度 |
| 同上 | Pool Capacity | 32、発射速度×弾寿命+1以上を目安にする |
| 生成フォルダーのEnemy.prefab | Max Health / Move Speed | 1 / 2.3 |
| 同上 | Hit Radius / Score Value | 0.5 / 10 |
| 生成フォルダーのBullet.prefab | Speed / Lifetime / Radius | 40 / 1.2秒 / 0.1 |

Enemy/BulletのPrefabは非アクティブのまま保存されています。実行時に必要な個体だけ有効にするための仕様です。Prefabをダブルクリックして値を編集できます。プールの数はPlay開始時に確定し、実行中の数値変更では増減しません。

## Windowsビルド

Playを停止し、生成シーンを開いた状態で **Last Line > Build Windows (Current Scene)** を選び、空の出力先フォルダーを指定します。現在のシーンだけをWindows 64-bit向けにビルドするので、Build Profilesへのシーン登録は不要です。必要なプラットフォーム対応がないというエラーが出た場合はUnity Hubの当該Editorのモジュールを確認してください。出力先の `LastLine.exe` と同時生成されたファイル一式を一緒に扱います。

ウィンドウの初期サイズはUnityプロジェクトのPlayer設定に従います。必要なら **Player > Resolution and Presentation** でWindowed / Width 540 / Height 960に設定してください。横長ウィンドウでもゲーム領域は9:16を維持します。

## 今回の検証範囲と次の段階

実施済みの結果は `docs/VALIDATION.md` と `docs/validation/rules.txt` に記録しています。初版はユーザーがUnity上で1ゲーム遊べたことを確認済みです。敵の増加もユーザーがPlayして確認済みです。演出追加版はUnity 6000.6.8f1参照でRuntime/Editorの独立コンパイルが成功しています。演出追加版のPlay・見た目・難易度・Windowsビルド・Android端末・100体負荷は未確認です。C#ルールの検証成功だけでUnity全体の動作確認済みとは扱っていません。

最小版で「射手が左右へ動く／撃破できる／突破で減る／終了と再試行ができる」を確認後、次の順で追加します。

1. 追加済みの3段階の出現間隔をPlayで確認し、負荷とライフのバランスを計測。
2. 追加済みのマズルフラッシュとプール式撃破エフェクトをPlayで確認し、見た目を調整。
3. UIをCanvas/UI Toolkitへ移し、日本語表示、Androidのセーフエリア、タッチ位置の調整。
4. Androidモジュール導入・実機入力・Profiler計測。100体上限時のフレーム時間とGCを確認して最適化。

音は後からAudioSourceを射手（旧シーンではTurret）に追加し、クリップを割り当て、MachineGunの **On Shot > +** にAudioSourceを指定して **AudioSource.Play()** を選ぶ形で追加できます。現在の版には音声素材は含みません。

参考にした公式API: [Unity 6 BuildPipeline.BuildPlayer](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/buildpipeline/buildplayer)、[Unity Input System 新旧API対応表](https://github.com/Unity-Technologies/InputSystem/blob/develop/Packages/com.unity.inputsystem/Documentation~/corresponding-old-new-api.md)。既存ゲームのコード・画像・キャラクターや外部アセットは使用していません。
