# PCCES 預算書外掛（Claude Code）

讓 Claude Code 幫你讀、查、改 PCCES 4.3 的預算書：看章節與總價、找工項、查鋼筋混凝土這類工料總共用多少、看單價分析的組成，或把數量單價改好另存一份 XML，再交給 PCCES 匯入；匯入後也可以請它幫你「重新總計」。

## 需要什麼

1. Windows 10 或 11（64 位元），已經裝好並登入的 Claude Code。
2. Git for Windows（Claude Code 用它下載外掛；沒裝的話 `安裝.bat` 會提示）。安裝本外掛不需要系統管理員權限，也不必另外裝 Python。
3. 想直接讀這台 PCCES 裡的預算書（不必先匯出），這台要裝 PCCES 4.3；連資料庫用的驅動會自動挑，不用另外安裝。
4. 沒裝 PCCES 也能用：PCCES 匯出的預算書 XML（檔名結尾 `_ap_bdgt.xml`）一樣能讀、能查、能改。

## 安裝

1. 下載這裡的 `安裝.bat`，雙擊執行。
2. 視窗最後出現 `Done.`，把 Claude Code 關掉重開。
3. 在 Claude Code 問「幫我讀這份預算書：C:\…\(預算書)…_ap_bdgt.xml」，它開始用 PCCES 工具就是裝好了。

雙擊後出現 `Claude Code (claude.exe) was not found`：改在 Claude Code 的對話框一行一行輸入下面兩行，再重開 Claude Code。

```text
/plugin marketplace add https://github.com/SodaCK87/pcces-plugin.git
/plugin install pcces@pcces-plugin
```

雙擊後出現 `Git for Windows was not found`：Claude Code 下載外掛要靠 Git，照視窗裡的網址裝好 Git for Windows（選項照預設），再雙擊一次 `安裝.bat`。

## 更新

雙擊 `更新.bat`，出現 `Done.` 後重開 Claude Code。它會先更新外掛清單、再更新外掛本身，兩步都跑完才會換到新版。

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
