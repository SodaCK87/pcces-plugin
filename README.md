# PCCES 預算書外掛（Claude Code）

讓 Claude Code 幫你讀、查、改 PCCES 4.3 的預算書：看章節與總價、找工項、查鋼筋混凝土這類工料總共用多少、看單價分析的組成，或把數量單價改好另存一份 XML，再交給 PCCES 匯入；匯入後也可以請它幫你「重新總計」。

<!-- 安裝說明：開始（build_install_guide.py 產生，別手改） -->
## 安裝

需要：
1. Windows 10 或 11（64 位元），已經裝好 Claude Code 並登入過。
2. Git for Windows：Claude Code 用它下載外掛，沒裝的話「安裝.bat」會告訴你怎麼裝。
3. 要直接讀這台 PCCES 裡的預算書，這台要裝 PCCES 4.3；沒裝 PCCES 也能讀、查、改 PCCES 匯出的預算書 XML。
4. 不需要系統管理員權限，也不用另外裝 Python。

步驟（做一次就好）：
1. 用瀏覽器打開 <https://github.com/SodaCK87/pcces-plugin>，在檔案清單裡點「安裝.bat」。
2. 找到右上角的「Raw」，按它右邊第二個、向下箭頭的按鈕（下載），存到「下載」資料夾。瀏覽器如果提醒這個檔案可能有害，選「保留」。
3. 到「下載」資料夾雙擊「安裝.bat」。如果跳出「Windows 已保護您的電腦」，先按「其他資訊」，再按「仍要執行」。
4. 黑色視窗最後出現「Done.」，按任一鍵關掉視窗。
5. 把 Claude Code 整個關掉，再重新打開。
6. 對 Claude 說「幫我讀這份預算書：」，後面貼上 PCCES 匯出的 XML 檔路徑（檔名結尾是 _ap_bdgt.xml）。它開始用 PCCES 工具回答，就是裝好了。

視窗出現下面的字時：

| 視窗出現的字 | 意思 | 怎麼辦 |
| --- | --- | --- |
| `Claude Code (claude.exe) was not found` | 這台找不到 Claude Code | 打開 Claude Code，在對話框一行一行輸入下面兩行，再重開 Claude Code：<br>`/plugin marketplace add https://github.com/SodaCK87/pcces-plugin.git`<br>`/plugin install pcces@pcces-plugin` |
| `Git for Windows was not found` | 這台沒有 Git，Claude Code 要用它下載外掛 | 到 <https://git-scm.com/download/win> 下載安裝，選項全部照預設，裝完再雙擊一次「安裝.bat」。 |
| `Install did not finish` | 安裝中途失敗 | 把整個黑色視窗截圖，傳給提供這個工具的人。 |

## 更新

1. 照安裝步驟第 1、2 步下載「更新.bat」（同一個檔案清單裡），雙擊執行。
2. 出現「Done.」後，把 Claude Code 關掉再重開，就換成新版了。
<!-- 安裝說明：結束 -->

## 怎麼用

1. 在 PCCES 匯出預算書：「檔案 → 製作電子檔及列印報表」，選「預算書」，勾「工程會電子標單檔 xml 格式」，存到找得到的資料夾。
2. 把檔案路徑貼給 Claude，請它讀整份、找某個工項、算某種工料總量，或改數量單價。
3. 改好的檔另存在原檔同一個資料夾，檔名像 `ArchEx001_edit_20261120_01.xml`，原檔不動。
4. 回到 PCCES：「專案建立‧轉入 → XML 電子檔轉入」匯入新檔，然後重新總計，公式項與資源統計這時才會更新。可以自己開預算書按「重新總計」；或把 PCCES 整個關掉，請 Claude 幫你重新總計（它會先跟你確認是哪一本）。
5. 要送出去的版本，在 PCCES 做自主檢查、再製作電子檔。

## 它會做、不會做

1. PCCES 的資料庫只讀；要改數量單價一律產生新的 XML，由 PCCES 自己匯入。唯一會寫資料庫的是「重新總計」：它叫 PCCES 自己的程式重算那一本，等於你在 PCCES 畫面上按「重新總計」。PCCES 開著、或 PCCES 版本跟驗證過的不同時，它不會動手，會告訴你怎麼做。
2. 不覆蓋你的原檔。
3. 產出的 XML 會標明是這個工具做的；PCCES 匯入時會提醒「非 PCCES 製作」，按確定就好。
4. 你請 Claude 直接操作 PCCES 視窗（例如幫你匯出）時，它會先問過你：操作期間會接管滑鼠和鍵盤。

## 授權與內附的程式

本外掛的程式以 MIT 授權釋出（見 `LICENSE`）。`plugins\pcces\recalc_host\` 是重新總計用的 32 位元小程式，原始碼 `RecalcHost.cs` 與怎麼編的紀錄 `BUILD.txt` 在同一個資料夾，同一份原始碼重編逐位元組相同。外掛裡附了一份 python.org 官方的可攜版 Python 3.13 與執行需要的套件，各自的授權檔留在原位：Python 的在 `plugins\pcces\python\LICENSE.txt`，各套件的在 `plugins\pcces\python\Lib\site-packages\` 底下各自的 `.dist-info` 資料夾。
