using ManageLiteAV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TRTCWPFDemo;
using TRTCWPFDemo.Common;

namespace TencentCloud_TRTC
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    

    public partial class MainWindow : Window, IDisposable, ITRTCCloudCallback
    {
        private ITRTCCloud mTRTCCloud;

        private bool mIsEnterSuccess;

        private string mUserId;          // ローカルユーザーID
        private uint mRoomId;            // 部屋番号

        private Dictionary<string, TXLiteAVVideoView> mVideoViews;

        public MainWindow()
        {
            InitializeComponent();

            this.Closed += MainWindow_Closed;

            mTRTCCloud = DataManager.GetInstance().trtcCloud;
            mVideoViews = new Dictionary<string, TXLiteAVVideoView>();

            // SDK設定の初期化およびコールバックの設定
            Log.I(String.Format(" SDKVersion : {0}", mTRTCCloud.getSDKVersion()));
            mTRTCCloud.addCallback(this);
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (mIsEnterSuccess)
            {
                this.ExitRoom();
            }
        }

        public void Dispose()
        {
            // クリアランスリソース
            mTRTCCloud = null;
        }

        public void EnterRoom()
        {
            // 入室時に必要な関連パラメータを設定する
            TRTCParams trtcParams = new TRTCParams();
            trtcParams.sdkAppId = GenerateTestUserSig.SDKAPPID;
            trtcParams.roomId = DataManager.GetInstance().roomId;
            trtcParams.userId = DataManager.GetInstance().userId;
            trtcParams.userSig = GenerateTestUserSig.GetInstance().GenTestUserSig(DataManager.GetInstance().userId);
            // 部屋のアクセス保護が必要な場合は、ドキュメント{https://cloud.tencent.com/document/product/647/32240}を参照して、この機能を完成させてください。
            // 部屋にアクセスできるユーザーのために、サーバーから取得したprivateMapKeyを以下のフィールドに追加します。
            trtcParams.privateMapKey = "";
            trtcParams.businessInfo = "";
            trtcParams.role = DataManager.GetInstance().roleType;
            // プロジェクトで音声のみのバイパスライブストリーミングが必要な場合、パラメータを設定する。
            // このパラメータを設定すると、音声がサーバーに到達し、自動的にバイパスが開始されます。
            // このパラメータがない場合、バイパスは最初のビデオフレームを受信する前に受信したオーディオパケットを破棄します。
            if (DataManager.GetInstance().pureAudioStyle)
                trtcParams.businessInfo = "{\"Str_uc_params\":{\"pure_audio_push_mod\": 1}}";
            else
                trtcParams.businessInfo = "";

            // ユーザーによる入退室
            mTRTCCloud.enterRoom(ref trtcParams, DataManager.GetInstance().appScene);

            // デフォルトのパラメータ構成を設定する
            TRTCVideoEncParam encParams = DataManager.GetInstance().videoEncParams;   // ビデオエンコードパラメーター設定
            TRTCNetworkQosParam qosParams = DataManager.GetInstance().qosParams;      // ネットワークフロー制御関連のパラメータ設定
            mTRTCCloud.setVideoEncoderParam(ref encParams);
            mTRTCCloud.setNetworkQosParam(ref qosParams);
            mTRTCCloud.setLocalViewFillMode(DataManager.GetInstance().videoFillMode);
            mTRTCCloud.setLocalViewMirror(DataManager.GetInstance().isLocalVideoMirror);
            mTRTCCloud.setLocalViewRotation(DataManager.GetInstance().videoRotation);
            mTRTCCloud.setVideoEncoderMirror(DataManager.GetInstance().isRemoteVideoMirror);

            // セットビューティー
            if (DataManager.GetInstance().isOpenBeauty)
                mTRTCCloud.setBeautyStyle(DataManager.GetInstance().beautyStyle, DataManager.GetInstance().beauty,
                    DataManager.GetInstance().white, DataManager.GetInstance().ruddiness);

            //サイズフローを設定する
            if (DataManager.GetInstance().pushSmallVideo)
            {
                TRTCVideoEncParam param = new TRTCVideoEncParam
                {
                    videoFps = 15,
                    videoBitrate = 100,
                    videoResolution = TRTCVideoResolution.TRTCVideoResolution_320_240
                };
                mTRTCCloud.enableSmallVideoStream(true, ref param);
            }
            if (DataManager.GetInstance().playSmallVideo)
            {
                mTRTCCloud.setPriorRemoteVideoStreamType(TRTCVideoStreamType.TRTCVideoStreamTypeSmall);
            }
            // 客室情報
            mUserId = trtcParams.userId;
            mRoomId = trtcParams.roomId;
            this.infoLabel.Content = "部屋番号：" + mRoomId;

            // ネイティブ・メインストリームのカスタムレンダリング ビューは動的にバインドされ、SDKのレンダリングコールバックをリッスンします。
            mTRTCCloud.startLocalPreview(IntPtr.Zero);
            AddCustomVideoView(this.videoContainer, mUserId, TRTCVideoStreamType.TRTCVideoStreamTypeBig, true);

            mTRTCCloud.startLocalAudio();
        }

        public void onUserVideoAvailable(string userId, bool available)
        {
            this.Dispatcher.BeginInvoke(new Action(() => {
                if (available)
                {
                    // リモートメインストリームのカスタムレンダリング ビューは動的にバインドされ、SDKのレンダリングコールバックをリッスンします。
                    mTRTCCloud.startRemoteView(userId, IntPtr.Zero);
                    AddCustomVideoView(this.videoContainer, userId, TRTCVideoStreamType.TRTCVideoStreamTypeBig);
                }
                else
                {
                    // リモートメインストリームカスタムレンダリング ビュー削除バインディング
                    mTRTCCloud.stopRemoteView(userId);
                    RemoveCustomVideoView(this.videoContainer, userId, TRTCVideoStreamType.TRTCVideoStreamTypeBig);
                }
            }));
        }

        public void onUserSubStreamAvailable(string userId, bool available)
        {
            this.Dispatcher.BeginInvoke(new Action(() => {
                if (available)
                {
                    // リモート補助ストリームのカスタムレンダリング ビューは動的にバインドされ、SDKレンダリングコールバックをリッスンします。
                    mTRTCCloud.startRemoteSubStreamView(userId, IntPtr.Zero);
                    AddCustomVideoView(this.videoContainer, userId, TRTCVideoStreamType.TRTCVideoStreamTypeSub);
                }
                else
                {
                    // リモート補助ストリーム カスタムレンダリング ビュー除去 バインディング
                    mTRTCCloud.stopRemoteSubStreamView(userId);
                    RemoveCustomVideoView(this.videoContainer, userId, TRTCVideoStreamType.TRTCVideoStreamTypeSub);
                }
            }));
        }

        /// <summary>
        /// カスタムレンダリングViewの追加とレンダーコールバックのバインド
        /// </summary>
        private void AddCustomVideoView(Panel parent, string userId, TRTCVideoStreamType streamType, bool local = false)
        {
            TXLiteAVVideoView videoView = new TXLiteAVVideoView();
            videoView.RegEngine(userId, streamType, mTRTCCloud, local);
            videoView.SetRenderMode(DataManager.GetInstance().videoFillMode);
            videoView.Width = 320;
            videoView.Height = 240;
            videoView.Margin = new Thickness(5, 5, 5, 5);
            parent.Children.Add(videoView);
            string key = String.Format("{0}_{1}", userId, streamType);
            mVideoViews.Add(key, videoView);
        }

        /// <summary>
        /// カスタムレンダリング View の削除とレンダリングコールバックのアンバインド
        /// </summary>
        private void RemoveCustomVideoView(Panel parent, string userId, TRTCVideoStreamType streamType, bool local = false)
        {
            TXLiteAVVideoView videoView = null;
            string key = String.Format("{0}_{1}", userId, streamType);
            if (mVideoViews.TryGetValue(key, out videoView))
            {
                videoView.RemoveEngine(mTRTCCloud);
                parent.Children.Remove(videoView);
                mVideoViews.Remove(key);
            }
        }

        public void ExitRoom()
        {
            Uninit();
            mTRTCCloud.exitRoom();
        }

        /// <summary>
        /// 内部SDK機能のチェックアウトおよびクローズ後に実行されるクリーンアップ操作。
        /// </summary>
        private void Uninit()
        {
            mTRTCCloud.stopAllRemoteView();
            mTRTCCloud.stopLocalPreview();
            foreach (var item in mVideoViews)
            {
                if (item.Value != null)
                {
                    item.Value.RemoveEngine(mTRTCCloud);
                    this.videoContainer.Children.Remove(item.Value);
                }
            }
            TXLiteAVVideoView.RemoveAllRegEngine();

            mTRTCCloud.stopLocalAudio();
            mTRTCCloud.muteLocalAudio(true);
            mTRTCCloud.muteLocalVideo(true);

            mTRTCCloud.removeCallback(this);
            mTRTCCloud.setLogCallback(null);
        }

        public void onError(TXLiteAVError errCode, string errMsg, IntPtr arg)
        {
            Log.E(String.Format("errCode : {0}, errMsg : {1}, arg = {2}", errCode, errMsg, arg));
        }

        public void onWarning(TXLiteAVWarning warningCode, string warningMsg, IntPtr arg)
        {
            Log.I(String.Format("warningCode : {0}, warningMsg : {1}, arg = {2}", warningCode, warningMsg, arg));
        }

        public void onEnterRoom(int result)
        {

        }

        public void onStartPublishing(int errCode, string errMsg)
        {
            Log.I(String.Format("errCode : {0}, errorMsg : {1}", errCode, errMsg));
        }
        public void onStopPublishing(int errCode, string errMsg)
        {
            Log.I(String.Format("errCode : {0}, errorMsg : {1}", errCode, errMsg));
        }
        public void onExitRoom(int reason)
        {
            mIsEnterSuccess = false;
            this.Close();
        }

        #region ITRTCCloudCallback
        public void onSwitchRole(TXLiteAVError errCode, string errMsg)
        {

        }

        public void onConnectOtherRoom(string userId, TXLiteAVError errCode, string errMsg)
        {

        }

        public void onDisconnectOtherRoom(TXLiteAVError errCode, string errMsg)
        {

        }

        public void onUserEnter(string userId)
        {

        }

        public void onUserExit(string userId, int reason)
        {

        }

        public void onRemoteUserEnterRoom(string userId)
        {

        }

        public void onRemoteUserLeaveRoom(string userId, int reason)
        {

        }

        public void onUserAudioAvailable(string userId, bool available)
        {

        }

        public void onFirstVideoFrame(string userId, TRTCVideoStreamType streamType, int width, int height)
        {

        }

        public void onFirstAudioFrame(string userId)
        {

        }

        public void onSendFirstLocalVideoFrame(TRTCVideoStreamType streamType)
        {

        }

        public void onSendFirstLocalAudioFrame()
        {

        }

        public void onNetworkQuality(TRTCQualityInfo localQuality, TRTCQualityInfo[] remoteQuality, uint remoteQualityCount)
        {

        }

        public void onStatistics(TRTCStatistics statis)
        {

        }

        public void onConnectionLost()
        {

        }

        public void onTryToReconnect()
        {

        }

        public void onConnectionRecovery()
        {

        }

        public void onSpeedTest(TRTCSpeedTestResult currentResult, uint finishedCount, uint totalCount)
        {

        }

        public void onCameraDidReady()
        {

        }

        public void onMicDidReady()
        {

        }

        public void onUserVoiceVolume(TRTCVolumeInfo[] userVolumes, uint userVolumesCount, uint totalVolume)
        {

        }

        public void onDeviceChange(string deviceId, TRTCDeviceType type, TRTCDeviceState state)
        {

        }

        public void onTestMicVolume(uint volume)
        {

        }

        public void onTestSpeakerVolume(uint volume)
        {

        }

        public void onRecvCustomCmdMsg(string userId, int cmdID, uint seq, byte[] msg, uint msgSize)
        {

        }

        public void onMissCustomCmdMsg(string userId, int cmdId, int errCode, int missed)
        {

        }

        public void onRecvSEIMsg(string userId, byte[] message, uint msgSize)
        {

        }

        public void onStartPublishCDNStream(int errCode, string errMsg)
        {

        }

        public void onStopPublishCDNStream(int errCode, string errMsg)
        {

        }

        public void onSetMixTranscodingConfig(int errCode, string errMsg)
        {

        }

        public void onScreenCaptureCovered()
        {

        }

        public void onScreenCaptureStarted()
        {

        }

        public void onScreenCapturePaused(int reason)
        {

        }

        public void onScreenCaptureResumed(int reason)
        {

        }

        public void onScreenCaptureStoped(int reason)
        {

        }

        public void onPlayBGMBegin(TXLiteAVError errCode)
        {

        }

        public void onPlayBGMProgress(uint progressMS, uint durationMS)
        {

        }

        public void onPlayBGMComplete(TXLiteAVError errCode)
        {

        }

        public void onAudioEffectFinished(int effectId, int code)
        {

        }

        #endregion

        private void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }

        public void onSwitchRoom(TXLiteAVError errCode, string errMsg)
        {
        }

        public void onAudioDeviceCaptureVolumeChanged(uint volume, bool muted)
        {
        }

        public void onAudioDevicePlayoutVolumeChanged(uint volume, bool muted)
        {
        }
    }
}
