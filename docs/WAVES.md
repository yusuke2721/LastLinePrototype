# 敵が増える版への更新

初版を遊べたことを受け、次の1段階として出現間隔だけを変更しました。Unityプロジェクト側のシーン・Prefab・Inspector調整はそのまま使えます。

## コピーする前に

1. Unity Editorで、上部のPlayボタンを押して **Playモードを停止**します。停止済みならそのままで構いません。
2. エクスプローラーでコピー元を開きます。
   `D:\GitHub\yusuke2721\LastLinePrototype\Assets\LastLine`
3. コピー元の **Scriptsフォルダー**をコピーし、次のフォルダーへ貼り付けます。同名ファイルの置き換えを選びます。
   `D:\UnityProjects\LastLine\LastLine\Assets\LastLine`
4. `Generated`のシーンやPrefabを作り直す必要はありません。Unityに戻り、コンパイルが終わるのを待ちます。
5. Hierarchyで **GameRoot > Enemy Pool** を選び、InspectorのEnemySpawnerで次の設定を確認します。

| 設定 | 値 |
|---|---|
| Increase Over Time | ON |
| Spawn Interval | 0.65 |
| Middle Spawn Interval | 0.3 |
| Final Spawn Interval | 0.1 |

以前から序盤のSpawn Intervalを調整していれば、その値が保持されます。新しく追加した項目は初期値が入ります。値を変えた場合はPlay前にCtrl+Sでシーンを保存します。

## 次の1ゲームで見ること

- 0〜20秒: 少なめの敵が出現する。
- 20〜40秒: 約2倍の速さで敵が出現する。
- 40〜60秒: 毎秒約10体のペースで敵が出現する（最大100体）。
- RETRY後は最初の段階に戻る。

時間の境界はRound Secondsの3等分です。60秒以外にすると境界も比例します。プールが満杯のときは新しい敵を出さず、空きができた後も待ち分をまとめて出しません。低FPSでは1フレーム1体が上限になるため、設定どおりの出現速度が出ない場合があります。

難しくて40秒まで確認できなければ、Playを止めてGameRootの **Starting Livesを100** にしてテストできます。確認後は10へ戻します。以前の一定間隔へ戻すには **Increase Over TimeをOFF** にします。

Play中の設定変更は停止時に戻ります。出現数と難易度の実測は引き続き必要です。マズルフラッシュ・撃破演出は追加済みです。[EFFECTS.md](EFFECTS.md) を参照してください。
