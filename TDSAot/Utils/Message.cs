using System;
using System.Runtime.InteropServices;
using TDSAot.State;

namespace TDSAot.Utils
{
    internal static class Message
    {
        private const uint yesNoType = MB_YESNO | MB_ICONQUESTION;

        internal static bool ShowYesNo(string title, string content)
        {
            StaticState.CanBeHide = false;

            try
            {
                int result = MessageBox(IntPtr.Zero, content, title, yesNoType);

                if (result == IDYES)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                StaticState.CanBeHide = true;
            }
        }

        internal static void ShowWaringOk(string title, string content)
        {
            StaticState.CanBeHide = false;
            try
            {
                MessageBox(IntPtr.Zero, content, title, MB_SYSTEMMODAL | MB_OK | MB_ICONINFORMATION);
            }
            finally
            {
                StaticState.CanBeHide = true;
            }
        }

        // 导入Windows API MessageBox函数
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        private const uint MB_SYSTEMMODAL = 0x00001000;

        // 消息框按钮选项
        private const uint MB_OK = 0x00000000;

        private const uint MB_OKCANCEL = 0x00000001;
        private const uint MB_YESNOCANCEL = 0x00000003;
        private const uint MB_YESNO = 0x00000004;

        // 消息框图标选项
        private const uint MB_ICONEXCLAMATION = 0x00000030;

        private const uint MB_ICONINFORMATION = 0x00000040;
        private const uint MB_ICONQUESTION = 0x00000020;
        private const uint MB_ICONERROR = 0x00000010;

        // 返回值常量
        private const int IDOK = 1;

        private const int IDCANCEL = 2;
        private const int IDYES = 6;
        private const int IDNO = 7;
    }
}