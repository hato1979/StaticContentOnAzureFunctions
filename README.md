# StaticContentOnAzureFunctions
Azure FunctionsでバンドルされたSPAのフロントエンド静的コンテンツを返すC# プログラムです。

以下のプロンプトで生成

## Azure Functions .Net10 Flex Consumptionsで稼働するContentディレクトリにバンドルされたSPAのフロントエンドの静的ファイルを返すプログラムを作ってください。
- インスタンス起動時にディレクトリ内のファイルをすべてメモリに読み込んで、リクエスト発生時はIOを行わずメモリから返すようにしてください。
- 存在しないパスがリクエストされた場合は404を返すのではなくindex.htmlにリダイレクトしてください。
- ファイルのSHA256からETagを生成してください。
- MIME Typeを拡張子から判定してContent-Typeを設定してください。なお文字コードはすべてUTF-8です。
- レスポンスにはCache-Controlヘッダ で1時間ブラウザ側でキャッシュさせるようにしてください。
- If-None-Matchヘッダを適切に処理してください。
- html、js、css、json、txtはインスタンス起動時にファイルから読み込む際にgzip版も生成して、リクエストのaccept-encodingにgzipがある場合はgzip版を優先して返してください。


