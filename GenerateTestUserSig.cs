using System;
using System.Text;
using TRTCWPFDemo.Common;
using tencentyun;
using System.Windows.Shapes;

/// <summary>
/// Module： GenerateTestUserSig
/// 
/// Function：テスト用のUserSigを生成するために使用します。UserSigは、Tencent Cloudが同社のクラウドサービス用に設計したセキュリティ保護署名です。
//            SDKAppID、UserID、EXPIRETIMEを暗号化アルゴリズムHMAC-SHA256で暗号化することにより算出される。
///      
/// Attention：以下の理由により、本アプリの公式オンライン版には以下のコードを公開しないでください。
/// 
///            このファイルのコードは、UserSigを正しく計算しますが、SDKの基本機能を素早くチューニングするのに適しているだけで、オンライン製品用ではありません。
///            これは、クライアント側のコードのSECRETKEYは簡単に逆コンパイルされてクラックされてしまうためで、特にウェブ側のコードはほとんどクラックすることができません。
///            キーが漏洩すると、攻撃者は正しいUserSigを計算し、Tencent Cloudのトラフィックを盗むことができるようになります。
///             
///            正しい方法は、UserSigの計算コードと暗号鍵を業務用サーバに置き、計算したUserSigをアプリがサーバからオンデマンドでリアルタイムに取得するようにすることです。
///            クライアントのAppよりもサーバーをクラックする方がコストがかかるので、サーバーで計算されたソリューションの方が暗号鍵の保護に優れています。
///            
/// Reference：https://cloud.tencent.com/document/product/647/17275#Server
/// </summary>

namespace TRTCWPFDemo
{
    class GenerateTestUserSig
    {
        /// <summary>
        /// Tencent Cloud SDKAppIdは、ご自身のアカウントのSDKAppIdに置き換える必要があります。
        /// 
        /// Tencent Cloud Communications [console](https://console.cloud.tencent.com/avc)にアクセスして、アプリケーションを作成すると、SDKAppId
        /// </summary>
        /// <remarks>
        /// Tencent Cloudが顧客を区別するために使用する一意の識別子です。
        /// </remarks>
        public const int SDKAPPID = 0;

        /// <summary>
        /// 署名用の暗号鍵の計算
        /// 
        /// Step1.Tencent Cloud Live Audio and Video[Console](https://console.cloud.tencent.com/rav)にアクセスし、まだアプリケーションがない場合は作成してください。
        /// Step2.アプリケーション設定」をクリックして基本設定画面を表示し、「勘定系システム連携」の項目を探します。
        /// Step3. "View Key "ボタンをクリックすると、UserSigの計算に使われた暗号鍵が表示されますので、それを以下の変数にコピーしてください。
        /// </summary>
        /// <remarks>
        /// 注意：このソリューションはデバッグデモ専用です。暗号化キーの漏洩によるトラフィックの盗難を避けるため、本稼働前にUserSigの計算コードとキーをバックエンドサーバーに移行してください。
        /// Documentation: https://cloud.tencent.com/document/product/647/17275#GetFromServer
        /// </remarks>
        public const string SECRETKEY = @"";

        /// <summary>
        /// 署名の有効期限は、あまり短く設定しないことを推奨します。
        /// 
        /// 時間単位：秒
        /// デフォルトの時間：7×24×60×60＝604800＝7日間
        /// </summary>
        public const int EXPIRETIME = 604800;

        /// <summary>
        /// このアカウント情報は、ミキサーインターフェース機能の実装に必要なものです。
        /// 
        /// アクセス：Tencent Cloud Web Console->Live Audio and Video->Your Application (eg Customer Service Call)->Account Information Panel can obtain appid/bizid.
        /// </summary>
        public const int APPID = 0;
        public const int BIZID = 0;

        private static GenerateTestUserSig mInstance;

        private GenerateTestUserSig()
        {
        }

        public static GenerateTestUserSig GetInstance()
        {
            if (mInstance == null)
            {
                mInstance = new GenerateTestUserSig();
            }
            return mInstance;
        }

        /// <summary>
        /// UserSig署名の計算
        /// 
        /// この関数は、内部で SDKAPPID、userId、EXPIRETIME を HMAC-SHA256 非対称暗号化アルゴリズムで暗号化します。
        /// 
        /// 
        /// </summary>
        /// <remarks>
        /// ソリューションは、ローカルランスルーデモや機能デバッグにのみ適している、製品は本当に秘密鍵がクラックされるのを避けるために、
        /// ソリューションを取得するためにサーバーを使用するために、オンラインでリリースされています。
        /// 
        ///このファイルのコードは、UserSigを正しく計算しますが、SDKの基本機能を素早くチューニングするのに適しているだけで、オンライン製品用ではありません。
        /// これは、クライアント側のコードのSECRETKEYは簡単に逆コンパイルされてクラックされてしまうためで、特にウェブ側のコードはほとんどクラックできないようになっています。
        /// キーが漏洩すると、攻撃者は正しいUserSigを計算し、Tencent Cloudのトラフィックを盗むことができるようになります。
        /// 
        /// 正しい方法は、UserSigの計算コードと暗号鍵を業務用サーバに置き、計算したUserSigをアプリがサーバからオンデマンドでリアルタイムに取得するようにすることです。
        /// クライアントのAppよりもサーバーをクラックする方がコストがかかるので、サーバーで計算されたソリューションの方が暗号鍵の保護に優れています。
        /// 
        /// Documentation: https://cloud.tencent.com/document/product/647/17275#GetFromServer
        /// </remarks>
        public string GenTestUserSig(string userId)
        {
            if (SDKAPPID == 0 || string.IsNullOrEmpty(SECRETKEY)) return null;
            TLSSigAPIv2 api = new TLSSigAPIv2(SDKAPPID, SECRETKEY);
            // SDK が内部で使用する UTF8 への統一的な変換。
            return api.GenSig(Util.UTF16To8(userId));
        }
        
    }
}
