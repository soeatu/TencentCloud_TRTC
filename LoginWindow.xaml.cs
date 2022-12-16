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
using System.Windows.Navigation;
using System.Windows.Shapes;
using TRTCWPFDemo;

namespace TencentCloud_TRTC
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            this.Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.userTextBox.Text = DataManager.GetInstance().userId;
            this.roomTextBox.Text = DataManager.GetInstance().roomId.ToString();
        }

        private void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }

        private void JoinBtn_Click(object sender, RoutedEventArgs e)
        {
            if (GenerateTestUserSig.SDKAPPID == 0)
            {
                ShowMessage("Error: GenerateTestUserSigにsdkappidの情報を先に記入してください。");
                return;
            }

            string userId = this.userTextBox.Text;
            string roomId = this.roomTextBox.Text;
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roomId))
            {
                ShowMessage("部屋番号やユーザー番号を空にすることはできません！");
                return;
            }

            DataManager.GetInstance().userId = userId;
            DataManager.GetInstance().roomId = uint.Parse(roomId);

            // ローカル計算から userId に対応する userSig を得る．
            // 注意! ローカル環境でのデバッグにはローカル計算が適しており、UserSigの計算コードと暗号化キーを業務サーバに置き、 // 必要に応じてアプリがサーバからUserSigを取得するのが正しい。
            // その後、Appは必要に応じてサーバーからリアルタイムに計算されたUserSigを取得します。
            // クライアントのAppよりもサーバーをクラックする方がコストがかかるため、サーバーで計算するソリューションの方が暗号鍵をより安全に保護することができます。
            string userSig = GenerateTestUserSig.GetInstance().GenTestUserSig(userId);
            if (string.IsNullOrEmpty(userSig))
            {
                ShowMessage("userSigの取得に失敗しました。アカウント情報が記入されているか確認してください！");
                return;
            }

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            mainWindow.EnterRoom();
            this.Close();
        }

        private void RoomTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool shiftKey = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (shiftKey == true)
            {
                e.Handled = true;
            }
            else
            {
                if (!((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) || e.Key == Key.Delete || e.Key == Key.Back || e.Key == Key.Tab || e.Key == Key.Enter))
                {
                    e.Handled = true;
                }
            }
        }
    }
}
