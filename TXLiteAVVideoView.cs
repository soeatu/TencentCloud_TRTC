using ManageLiteAV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TRTCWPFDemo.Common;

/// <summary>
/// WPFフレームワークによって実装され、TRTC SDKに関連付けられたカスタムビデオレンダリングView。
//// 
/// 提供される主なAPIは以下の通りである。
/// 1.RegEngine(string userId, TRTCVideoStreamType type, ITRTCCloud engine, bool local = false);
/// 2.RemoveEngine(ITRTCCloud engine)。
/// 3.SetRenderMode（TRTCVideoFillModeモード）。
/// 4.IsViewOccupy();
/// 5.SetPause(bool pause);
/// 6.RemoveAllRegEngine()を実行。
//// 
//// 主な用途は以下の通りです。
/// 1.ローカル画面レンダリング。
/// TXLiteAVVideoView.RegEngine(localUserId, type, engine, true)を介してstartLocalPreview(IntPtr.Zero)を開く前後。
/// SDK レンダリングコールバックを View にバインドして設定し、表示する必要のある親 View に View を追加します。
/// 2.リモート画面レンダリング。
/// Open startRemoteView(remoteUserId, IntPtr.Zero) または startRemoteSubStreamView(remoteUserId, IntPtr.Zero) を実行します．
/// TXLiteAVVideoView.RegEngine(remoteUserId, type, engine, true)の前または後。
/// このViewにバインドされたSDKレンダリングコールバックを設定し、表示する必要がある親Viewに単純にViewを追加します。
/// 3.スクリーンを取り外す。
/// ユーザーの退室時、またはリモートユーザーの退室時に、TXLiteAVVideoView.RemoveEngine(engine)を使用します。
/// </summary>
/// <備考>
/// このインターフェースはスレッドセーフではないので、メインスレッドで呼び出してください。
/// 
/// 注：リモートメインと補助ストリームは setRemoteVideoRenderCallback を一度だけ設定する必要があるので、リモートユーザがチェックアウトするときにリスナーを一律に削除するのが最善です。
/// そうでなければ、データのストリームでunlistenの呼び出しが行われたとき、そのユーザーの他のストリームはデータを受け取ることができません。
/// </remarks>。

namespace TRTCWPFDemo
{
    public class TXLiteAVVideoView : Canvas, IDisposable
    {
        private bool mOccupy = false;     // view 是否已被占用
        private bool mLocalView = false;  // 是否为本地画面
        private bool mPause = false;
        private bool mFirstFrame = false;

        private string mUserId;
        private TRTCVideoStreamType mStreamType;
        private TRTCVideoFillMode mRenderMode = TRTCVideoFillMode.TRTCVideoFillMode_Fit;  // 0：填充，1：适应

        private volatile FrameBufferInfo mArgbFrame = new FrameBufferInfo();  // 帧缓存

        // 位图缓存，防止GC频繁
        private WriteableBitmap mWriteableBitmap;  
        private Int32Rect mInt32Rect;
        private Pen mPen;

        public TXLiteAVVideoView()
        {
            mPen = new Pen { Brush = Brushes.DarkGray, Thickness = 1 };
        }

        /// <summary>
        /// 设置 View 绑定参数
        /// </summary>
        /// <param name="userId">需要渲染画面的 userId，如果是本地画面，则传空字符串。</param>
        /// <param name="type">渲染类型</param>
        /// <param name="engine">TRTCCloud 实例，用户注册视频数据回调。</param>
        /// <param name="local">渲染本地画面，SDK 返回的 userId 为""</param>
        /// <returns>true：绑定成功，false：绑定失败</returns>
        public bool RegEngine(string userId, TRTCVideoStreamType type, ITRTCCloud engine, bool local = false)
        {
            if (mOccupy) return false;
            mLocalView = local;
            mUserId = userId;
            mStreamType = type;
            int count = TXLiteAVVideoViewManager.GetInstance().Count;
            if (engine != null)
            {
                if (count == 0)
                {
                    engine.setLocalVideoRenderCallback(TRTCVideoPixelFormat.TRTCVideoPixelFormat_BGRA32,
                        TRTCVideoBufferType.TRTCVideoBufferType_Buffer, TXLiteAVVideoViewManager.GetInstance());
                }
                if (!mLocalView)
                {
                    engine.setRemoteVideoRenderCallback(userId, TRTCVideoPixelFormat.TRTCVideoPixelFormat_BGRA32,
                        TRTCVideoBufferType.TRTCVideoBufferType_Buffer, TXLiteAVVideoViewManager.GetInstance());
                }
            }
            if (mLocalView)
                TXLiteAVVideoViewManager.GetInstance().AddView("", type, this);
            else
                TXLiteAVVideoViewManager.GetInstance().AddView(userId, type, this);
            lock (mArgbFrame)
                ReleaseBuffer(mArgbFrame);
            mOccupy = true;
            this.InvalidateVisual();
            return true;
        }

        /// <summary>
        /// 移除 View 绑定参数
        /// </summary>
        /// <param name="engine">TRTCCloud 实例，用户注册视频数据回调。</param>
        public void RemoveEngine(ITRTCCloud engine)
        {
            if (mLocalView)
                TXLiteAVVideoViewManager.GetInstance().RemoveView("", mStreamType, this);
            else
                TXLiteAVVideoViewManager.GetInstance().RemoveView(mUserId, mStreamType, this);
            int count = TXLiteAVVideoViewManager.GetInstance().Count;
            if (engine != null)
            {
                if (count == 0)
                {
                    engine.setLocalVideoRenderCallback(TRTCVideoPixelFormat.TRTCVideoPixelFormat_Unknown,
                        TRTCVideoBufferType.TRTCVideoBufferType_Unknown, null);
                }
                if (!mLocalView && !TXLiteAVVideoViewManager.GetInstance().HasUserId(mUserId))
                {
                    engine.setRemoteVideoRenderCallback(mUserId, TRTCVideoPixelFormat.TRTCVideoPixelFormat_Unknown,
                        TRTCVideoBufferType.TRTCVideoBufferType_Unknown, null);
                }
            }
            lock (mArgbFrame)
                ReleaseBuffer(mArgbFrame);
            mUserId = "";
            mOccupy = false;
            mLocalView = false;
            mFirstFrame = false;
            mRenderMode = TRTCVideoFillMode.TRTCVideoFillMode_Fit;
            this.InvalidateVisual();
        }

        /// <summary>
        /// 设置 View 的渲染模式
        /// </summary>
        /// <param name="mode">渲染模式</param>
        public void SetRenderMode(TRTCVideoFillMode mode)
        {
            mRenderMode = mode;
        }

        /// <summary>
        /// 判断 View 是否被占用
        /// </summary>
        /// <returns>true：当前 View 已被占用，false：当前 View 未被占用</returns>
        public bool IsViewOccupy()
        {
            return mOccupy;
        }

        /// <summary>
        /// 暂停渲染，显示默认图片
        /// </summary>
        /// <param name="pause">是否暂停</param>
        public void SetPause(bool pause)
        {
            if (mPause != pause)
            {
                mPause = pause;
                if (mPause)
                {
                    this.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x20, 0x20));
                }
                else
                {
                    this.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
                    // 避免刷新最后一帧数据
                    lock (mArgbFrame)
                        ReleaseBuffer(mArgbFrame);
                }
                this.InvalidateVisual();
            }
        }

        /// <summary>
        /// 清除所有映射信息
        /// </summary>
        public static void RemoveAllRegEngine()
        {
            TXLiteAVVideoViewManager.GetInstance().RemoveAllView();
        }

        public bool AppendVideoFrame(byte[] data, int width, int height, TRTCVideoPixelFormat videoFormat, TRTCVideoRotation rotation)
        {
            if (!mFirstFrame)
                mFirstFrame = true;
            if (mPause)
                return false;
            if (data == null || data.Length <= 0)
                return false;
            // data 数据有误
            if (videoFormat == TRTCVideoPixelFormat.TRTCVideoPixelFormat_BGRA32 && width * height * 4 != data.Length)
                return false;
            // 暂时不支持其他 YUV 类型
            if (videoFormat == TRTCVideoPixelFormat.TRTCVideoPixelFormat_I420 && width * height * 3 / 2 != data.Length)
                return false;

            // 目前只实现了 BGRA32 的数据类型，如需其他类型请重写，并设置回调的数据类型
            if (videoFormat == TRTCVideoPixelFormat.TRTCVideoPixelFormat_BGRA32)
            {
                lock (mArgbFrame)
                {
                    if (mArgbFrame.data == null || mArgbFrame.width != width || mArgbFrame.height != height)
                    {
                        ReleaseBuffer(mArgbFrame);
                        mArgbFrame.width = width;
                        mArgbFrame.height = height;
                        mArgbFrame.data = new byte[data.Length];
                    }
                    Buffer.BlockCopy(data, 0, mArgbFrame.data, 0, (int)data.Length);
                    mArgbFrame.newFrame = true;
                    mArgbFrame.rotation = rotation;
                }
            }

            // 回到主线程刷新当前画面
            this.Dispatcher.Invoke(new Action(() =>
            {
                this.InvalidateVisual();
            }));

            return true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            bool needDrawFrame = true;
            if (mPause)
                needDrawFrame = false;
            if (mArgbFrame.data == null)
                needDrawFrame = false;
            if (!needDrawFrame)
            {
                return;
            }

            lock (mArgbFrame)
            {
                if (mArgbFrame.data == null)
                    return;
                if (mRenderMode == TRTCVideoFillMode.TRTCVideoFillMode_Fill)
                {
                    RenderFillMode(dc, mArgbFrame.data, mArgbFrame.width, mArgbFrame.height, (int)mArgbFrame.rotation * 90);
                }
                else if (mRenderMode == TRTCVideoFillMode.TRTCVideoFillMode_Fit)
                {
                    RenderFitMode(dc, mArgbFrame.data, mArgbFrame.width, mArgbFrame.height, (int)mArgbFrame.rotation * 90);
                }
            }
        }

        private void RenderFillMode(DrawingContext dc, byte[] data, int width, int height, int rotation)
        {
            int viewWidth = (int)this.ActualWidth, viewHeight = (int)this.ActualHeight;
            PixelFormat pixelFormat = PixelFormats.Pbgra32;
            int bytesPerPixel = (pixelFormat.BitsPerPixel + 7) / 8;
            int stride = bytesPerPixel * width;
            if (mWriteableBitmap == null || mWriteableBitmap.PixelWidth != width || mWriteableBitmap.PixelHeight != height)
            {
                mWriteableBitmap = new WriteableBitmap(width, height, 96, 96, pixelFormat, null);
                mInt32Rect = new Int32Rect(0, 0, width, height);
            }
            mWriteableBitmap.Lock();
            Marshal.Copy(data, 0, mWriteableBitmap.BackBuffer, data.Length);
            mWriteableBitmap.AddDirtyRect(mInt32Rect);
            mWriteableBitmap.Unlock();

            ImageBrush brush = new ImageBrush(mWriteableBitmap);
            if (rotation > 0)
            {
                Matrix transform = Matrix.Identity;
                double scale = (double)viewWidth / (double)viewHeight;
                if (rotation == 90 || rotation == 270)
                    transform.ScaleAt(scale, scale, 0.5, 0.5);
                transform.RotateAt(rotation, 0.5, 0.5);
                brush.RelativeTransform = new MatrixTransform(transform);
            }
            brush.Stretch = Stretch.UniformToFill;
            Rect rect = new Rect(0, 0, viewWidth, viewHeight);
            dc.DrawRectangle(brush, mPen, rect);
        }

        private void RenderFitMode(DrawingContext dc, byte[] data, int width, int height, int rotation)
        {
            int viewWidth = (int)this.ActualWidth, viewHeight = (int)this.ActualHeight;
            PixelFormat pixelFormat = PixelFormats.Pbgra32;
            int bytesPerPixel = (pixelFormat.BitsPerPixel + 7) / 8;
            int stride = bytesPerPixel * width;
            if (mWriteableBitmap == null || mWriteableBitmap.PixelWidth != width || mWriteableBitmap.PixelHeight != height)
            {
                mWriteableBitmap = new WriteableBitmap(width, height, 96, 96, pixelFormat, null);
                mInt32Rect = new Int32Rect(0, 0, width, height);
            }
            mWriteableBitmap.Lock();
            Marshal.Copy(data, 0, mWriteableBitmap.BackBuffer, data.Length);
            mWriteableBitmap.AddDirtyRect(mInt32Rect);
            mWriteableBitmap.Unlock();

            ImageBrush brush = new ImageBrush(mWriteableBitmap);
            if (rotation > 0)
            {
                Matrix transform = Matrix.Identity;
                double scale = (double)viewHeight / (double)viewWidth;
                if (rotation == 90 || rotation == 270)
                    transform.ScaleAt(1, scale, 0.5, 0.5);
                transform.RotateAt(rotation, 0.5, 0.5);
                brush.RelativeTransform = new MatrixTransform(transform);
            }
            brush.Stretch = Stretch.Uniform;
            Rect rect = new Rect(0, 0, viewWidth, viewHeight);
            dc.DrawRectangle(brush, mPen, rect);
        }

        private void ReleaseBuffer(FrameBufferInfo info)
        {
            if (info.data != null)
                info.data = null;
            info.width = 0;
            info.height = 0;
            info.newFrame = false;
            info.rotation = TRTCVideoRotation.TRTCVideoRotation0;
        }

        #region Dispose
        private bool disposed = false;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                ReleaseBuffer(mArgbFrame);
                mWriteableBitmap = null;
            }
            disposed = true;
        }

        ~TXLiteAVVideoView()
        {
            this.Dispose(false);
        }
        #endregion
    }

    class TXLiteAVVideoViewManager : ITRTCVideoRenderCallback
    {
        private volatile Dictionary<string, TXLiteAVVideoView> mMapViews;

        public static TXLiteAVVideoViewManager sInstance;

        private static Object mLocker = new Object();

        public static TXLiteAVVideoViewManager GetInstance()
        {
            if (sInstance == null)
            {
                lock (mLocker)
                {
                    if (sInstance == null)
                        sInstance = new TXLiteAVVideoViewManager();
                }
            }
            return sInstance;
        }

        private TXLiteAVVideoViewManager()
        {
            mMapViews = new Dictionary<string, TXLiteAVVideoView>();
        }

        private string GetKey(string userId, TRTCVideoStreamType type)
        {
            return String.Format("{0}_{1}", userId, type);
        }

        // 主要用于判断当前 user 是否还有存在流画面，存在则不移除监听。
        public bool HasUserId(string userId)
        {
            bool exit = false;
            lock (mMapViews)
            {
                exit = mMapViews.ContainsKey(GetKey(userId, TRTCVideoStreamType.TRTCVideoStreamTypeBig)) ||
                    mMapViews.ContainsKey(GetKey(userId, TRTCVideoStreamType.TRTCVideoStreamTypeSub));
            }
            return exit;
        }

        public void AddView(string userId, TRTCVideoStreamType type, TXLiteAVVideoView view)
        {
            lock (mMapViews)
            {
                bool find = false;
                foreach (var item in mMapViews)
                {
                    if (item.Key.Equals(GetKey(userId, type)))
                    {
                        find = true;
                        break;
                    }
                }
                if (!find)
                {
                    mMapViews.Add(GetKey(userId, type), view);
                }
            }
        }

        public void RemoveView(string userId, TRTCVideoStreamType type, TXLiteAVVideoView view)
        {
            lock (mMapViews)
            {
                foreach (var item in mMapViews.ToList())
                {
                    if (item.Key.Equals(GetKey(userId, type)))
                    {
                        if (item.Value != null)
                        {
                            item.Value.Dispose();
                        }
                        mMapViews.Remove(item.Key);
                        break;
                    }
                }
            }
        }

        public void RemoveAllView()
        {
            lock (mMapViews)
                mMapViews.Clear();
        }

        public int Count
        {
            get
            {
                lock (mMapViews)
                    return mMapViews.Count;
            }
        }

        public void onRenderVideoFrame(string userId, TRTCVideoStreamType streamType, TRTCVideoFrame frame)
        {
            // 大小视频是占一个视频位，底层支持动态切换。
            if (streamType == TRTCVideoStreamType.TRTCVideoStreamTypeSmall)
                streamType = TRTCVideoStreamType.TRTCVideoStreamTypeBig;
            TXLiteAVVideoView view = null;
            lock (mMapViews)
            {
                foreach (var item in mMapViews)
                {
                    if (item.Key.Equals(GetKey(userId, streamType)) && item.Value != null)
                    {
                        view = item.Value;
                        break;
                    }
                }
            }
            if (view != null)
                view.AppendVideoFrame(frame.data, (int)frame.width, (int)frame.height, frame.videoFormat, frame.rotation);
        }
    }

    class FrameBufferInfo
    {
        public byte[] data { get; set; }

        public int width { get; set; }

        public int height { get; set; }

        public bool newFrame { get; set; }

        public TRTCVideoRotation rotation { get; set; }

        public FrameBufferInfo()
        {
            rotation = TRTCVideoRotation.TRTCVideoRotation0;
            newFrame = false;
            width = 0;
            height = 0;
            data = null;
        }
    }
}
