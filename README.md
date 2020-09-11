# ChromeBookmarksCheckUrl
Chrome 的書籤URL檢查，會將無法連結的網址，匯出成 Excel 檔，可以人工確認沒問題，再去把網址刪了。

程式有使用 LiteDB 將確認沒問題的網址存入，下次再檢查時，可以跳過沒問題的網址，加快檢查速度。

如果要重新檢查全部網址，請刪除 \bin\Debug\CheckOK.db

相關路徑設定，請修改 App.config

程式使用的套件

Newtonsoft.Json

EPPlus

LiteDB
