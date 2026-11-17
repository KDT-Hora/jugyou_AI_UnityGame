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

## 不具合と対応

- 擬似入力(`simulate_key`)は押下状態だけを書き込み、`wasPressedThisFrame` が立たなかった。Rは `isPressed` の立ち上がり検出に変更。
- CLEARテキストが枠高さ不足で非表示だった。枠を拡大しOverflowに設定して解消。

## 未確認

- S/D キーでの移動方向の個別確認、実キーボードでのR操作
- EditModeテスト(任意項目)
