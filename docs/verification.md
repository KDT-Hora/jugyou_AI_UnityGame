# BallCollect 検証記録

Editor 6000.6.4f1 / Play モード中に `unity command runtime simulate_key` で入力し、`script eval` で状態を取得。

| 条件 | 操作 | 期待 | 実際 | 根拠 | 判定 |
|---|---|---|---|---|---|
| B1 初期 | Play開始 | 球(0,0.5,-7)、0/5、CLEAR非表示、アイテム5 | その通り | 状態取得 + `evidence/b1_initial.png` | 確認済み |
| B2 移動 | W/A押下 | W=+Z、A=-X | Z:-7→-1.84(W約0.8秒)、A押下でX減少 | 前後のTransform | 確認済み(S・Dは未確認、Dは落下確認で右移動を使用) |
| B3 回収 | Wで進む | 触れると消え+1、二重加算なし | 2個で cnt=2/items=3、5個で cnt=5 | 状態取得 | 確認済み |
| B4 クリア | 5個回収 | 5/5でCLEAR、途中は非表示 | cnt=2 でclearUI=False、5個でCLEAR表示 | `evidence/b4_clear.png` | 確認済み |
| B5 落下 | Dで右端から落下 | 位置・速度・回収数・アイテム・UI初期化 | (0,0.5,-7) cnt=0 items=5 | 状態取得 | 確認済み |
| B6 R(途中) | 1個回収後にR | 初期状態へ | cnt=0 items=5 pos=(0,0.5,-7) | 状態取得 | 確認済み |
| B6 R(クリア後) | CLEAR後にR | 初期状態へ | cnt=0 clearUI=False items=5 | 状態取得 | 確認済み |

## 自動テスト結果

`unity command test run`(Editor内実行)。**EditMode 5/5 合格、PlayMode 6/6 合格、失敗0・スキップ0**。

- EditMode(`Assets/Tests/EditMode/GameLogicTests.cs`): 初期状態 / 同一アイテム二重回収で1 / 4個では未クリア・5個でクリア / ResetGameで全初期化 / null回収で無変化
- PlayMode(`Assets/Tests/PlayMode/GameplayTests.cs`、仮想キーボード): W=+Z、A=-X・D=+X、S=-Z、W長押しで5個回収しクリア、R途中リセット、床から落下でリセット
- 初回のEditMode実行は5件全失敗: `PlayerController` が `Awake` で取得する Rigidbody が EditMode では未初期化(NullReference)。遅延取得に修正して合格。

## 30秒制限(A1〜A5)の検証

自動テスト: EditMode 12/12、PlayMode 8/8 合格(時間関連は EditMode 7件+PlayMode 2件を追加)。
実時間30秒での実機確認(短縮なし、`unity command` で入力と状態取得):

| 条件 | 操作 | 期待 | 実際 | 根拠 | 判定 |
|---|---|---|---|---|---|
| A1 | 開始後5秒待つ | 表示が減る | 27.0→21.9秒、`TIME 22` | 状態取得 + `evidence/a1_timer.png` | 確認済み |
| A2 | 操作せず約33秒 | TIME UP、移動停止 | remain=0、TIME UP表示。W1秒押しても座標不変 | `evidence/a2_timeup.png` | 確認済み |
| A3 | 約3.7秒で全回収 | CLEARで時計停止 | 26.27で3秒後も26.27 | 状態取得 | 確認済み |
| A4 | CLEAR後30秒待つ | TIME UPにならない | timeUp=False、CLEAR表示のまま | 状態取得 | 確認済み |
| A5 | 時間切れ後R / CLEAR後R | 30秒に戻る | 約28.7(0.8秒後に取得)、UIも初期化 | 状態取得 | 確認済み |
| 優先順位 | 残り29.99秒で5個回収→Tick | CLEAR優先 | CLEAR | EditModeテスト | 確認済み(実機で同時成立は未再現) |
| 落下でも30秒へ | 落下 | 30秒に戻る | `ResetGame` を共用(旧テストで落下リセットは確認済み) | コード+既存テスト | 実機の時計値は未確認 |

注意: 実機確認はEditorが非フォーカスでも進む設定で行った。TIME UP文字がアイテムと重なる(見た目の軽微な課題)。クリア後の入力停止(前回の問題1)は今回未対応。

## クリア後の入力停止(問題点1の対応)

`PlayerController` の移動を `manager.IsPlaying`(クリアでも時間切れでもない間)に限定。Rは常に有効。
- 自動テスト追加: `AfterClear_InputIsIgnored_ClearStaysUntilR`(クリア後にW1.5秒・A・Sを押しても座標不変・CLEAR維持、Rで再開)。EditMode 12/12、PlayMode 9/9 合格。
- 実機: クリア後にW3秒+さらに3秒押し続け、Aも1.5秒追加 → 座標(0,0.5,7.15)のまま、CLEAR表示と残り時間24.64は不変。Rで(0,0.5,-7)・0/5・30秒へ復帰。以前はW長押しで奥の端から落ちてCLEARが消えていた。根拠画像 `evidence/after_clear_input_locked.png`。

## 画面の派手化(ドパガキ向け演出)

ロジックには触れず、`GameManager` のイベント(`ItemCollected` / `GameCleared` / `TimeUpReached` / `GameReset`)を `Juice` が聞いて演出する構成。
- 見た目: 紫の背景、ピンク×水色のチェッカー床、光るピンクの球+虹色トレイル、虹色に脈打って浮くアイテム、漂うキラキラ、ブルーム・彩度・ビネット。
- 演出: 回収で粒子爆発+カウント拡大+画面揺れ、CLEARで紙吹雪+虹色に跳ねる文字+大きな揺れ、残り10秒以下で時間が赤く脈打つ、TIME UPで文字が震えて画面揺れ。
- 確認: EditMode 12/12・PlayMode 9/9 合格のまま。Play中のコンソールは警告・エラー0。画像 `evidence/pop_1_initial.png` `pop_2_collect.png` `pop_3_clear.png`。
- 途中で直した点: パーティクル速度カーブのモード不一致エラー(毎フレーム出ていた)、ブルーム過多によるアイテムの白飛び。
- 未確認/未実施: 残り10秒の赤点滅とTIME UPの震えは画像で未確認(コードのみ)。効果音は範囲外のため未実装(入れると更にそれらしくなる)。

## パチンコ/スロット風演出(ドパガキ演出を差し替え)

ロジックは変更なし。`Juice`(演出)と `ItemFx`(コイン)が `GameManager` の状態・イベントを読むだけ。
- 盤面: 紫のネオングリッド床、周囲84個の電飾が走る(クリアで全点滅、時間切れで赤)、回転する放射状サンバースト背景(色が回り、クリアで金/ピンク点滅、時間切れで赤)。
- 玉とコイン: 銀色のパチンコ玉+金のトレイル。アイテムは金色のコインが回転して浮く。
- 3リール(画面下): 回収1・3・5個目で順に「7」が止まり、5個目で「777」+「JACKPOT 777」看板が点滅。時間切れは「X X X」。カウントは回収時にスロットのように数字が回ってから確定。
- 演出: 回収で金の粒子・黄色フラッシュ・ズーム、クリアで虹のストロボ・紙吹雪・コインのシャワー・大きな揺れ、時間切れで赤フラッシュ。
- 確認: EditMode 12/12・PlayMode 9/9 合格のまま、Play中のコンソールは警告・エラー0。画像 `evidence/slot_1_initial.png` `slot_2_collect.png` `slot_3_clear.png` `slot_4_jackpot.png` `slot_5_timeup.png`。
- 注意: `slot_5_timeup.png` は時間切れの見た目確認のため `Tick(100)` で時間を進めて撮影(30秒待ちではない。30秒待ちの実機確認は前述A2で実施済み)。
- 未実施: 効果音(範囲外)。実キーボード・ビルド実機での見た目。点滅が強いため、光過敏の人への注意が必要(ストロボは約1.8秒、毎秒9回)。

## 動画

`evidence/playthrough.mp4`(約24秒、Editor画面)。D→A→S→W(回収)→R→全回収でCLEAR→クリア後R→回収後に右端から落下→リセット。
各操作後の座標・回収数は `evidence/playthrough_log.txt`。ログと動画の内容は一致。

## 洗い出した問題点

| # | 問題 | 重さ | 根拠 |
|---|---|---|---|
| 1 | **CLEAR後も操作でき、そのまま落ちるとCLEARが消える**。W長押しで奥の端(Z=10)から落下→0/5へ全リセット。1回目の録画で再現(W 3秒→CLEAR出ずリセット) | 高 | 録画ログ(初回) |
| 2 | 落下の予兆がない。壁・縁の目印がなく、床の端で突然リセットされる | 中 | 動画 |
| 3 | 奥側ではカメラが遠く、球とアイテムが小さい。CLEAR文字が球の真上に重なる | 中 | `b4_clear.png` |
| 4 | 擬似入力は `wasPressedThisFrame` が立たない。Rは立ち上がり検出+`wasPressedThisFrame`併用に修正済み。実キーボードでの確認は未実施 | 中 | 実験で再現 |
| 5 | 速度を直接代入しており、慣性・加速がない(止まると即停止)。球らしい挙動ではない | 低 | 速度ログ vel=0 |
| 6 | Game ビューが非フォーカスだと実キー入力が無効になる設定(`PointersAndKeyboardsRespectGameViewFocus`)。Editor操作時の注意点 | 低 | Input System設定 |
| 7 | UIは旧 `UnityEngine.UI.Text`+組込フォント。日本語表示や解像度対応が弱い | 低 | コード |
| 8 | ビルド/実機未検証(範囲外)。クラウド未連携 | 情報 | - |

問題1・2は仕様(B5)上は正しい挙動だが、遊びとしては不親切。要判断: クリア後は入力を止めるか、床に壁を付けるか。

## 不具合と対応

- 擬似入力(`simulate_key`)は押下状態だけを書き込み、`wasPressedThisFrame` が立たなかった。Rは `isPressed` の立ち上がり検出に変更。
- CLEARテキストが枠高さ不足で非表示だった。枠を拡大しOverflowに設定して解消。

## 未確認

- S/D キーでの移動方向の個別確認、実キーボードでのR操作
- EditModeテスト(任意項目)
